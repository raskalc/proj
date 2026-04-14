using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using AssemblyManager.Models;

namespace AssemblyManager.Services
{
    public class AssemblyRepository
    {
        private readonly string _connectionString;
        private readonly string _masterConnectionString;

        public AssemblyRepository(string? connectionString = null, string? masterConnectionString = null)
        {
            _connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["AssemblyDb"]?.ConnectionString
                ?? "Data Source=185.246.223.49,1433;Initial Catalog=AssemblyManager;User ID=sa;Password=123123gG@!;TrustServerCertificate=True;";
            _masterConnectionString = masterConnectionString ?? ConfigurationManager.ConnectionStrings["MasterDb"]?.ConnectionString
                ?? "Data Source=185.246.223.49,1433;Initial Catalog=master;User ID=sa;Password=123123gG@!;TrustServerCertificate=True;";
        }

        public async Task EnsureCreatedAsync()
        {
            await CreateDatabaseIfMissingAsync();
            await CreateTablesIfMissingAsync();
        }

        private async Task CreateDatabaseIfMissingAsync()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            var dbName = builder.InitialCatalog;

            using var master = new SqlConnection(_masterConnectionString);
            await master.OpenAsync();

            using var command = master.CreateCommand();
            command.CommandText = $"IF DB_ID(@db) IS NULL CREATE DATABASE [{dbName}];";
            command.Parameters.AddWithValue("@db", dbName);
            await command.ExecuteNonQueryAsync();
        }

        private async Task CreateTablesIfMissingAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var createAssemblies = @"
                IF OBJECT_ID('dbo.Assemblies','U') IS NULL
                BEGIN
                    CREATE TABLE dbo.Assemblies
                    (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(100) NOT NULL,
                        Code NVARCHAR(50) NULL,
                        Description NVARCHAR(500) NULL,
                        ModelFileName NVARCHAR(260) NULL,
                        ModelData VARBINARY(MAX) NULL,
                        CreatedAt DATETIME2 NOT NULL,
                        UpdatedAt DATETIME2 NOT NULL
                    );
                END
                ELSE
                BEGIN
                    IF COL_LENGTH('dbo.Assemblies','ModelPath') IS NOT NULL
                        ALTER TABLE dbo.Assemblies DROP COLUMN ModelPath;
                    IF COL_LENGTH('dbo.Assemblies','ModelFileName') IS NULL
                        ALTER TABLE dbo.Assemblies ADD ModelFileName NVARCHAR(260) NULL;
                    IF COL_LENGTH('dbo.Assemblies','ModelData') IS NULL
                        ALTER TABLE dbo.Assemblies ADD ModelData VARBINARY(MAX) NULL;
                END";

            var createParts = @"
                IF OBJECT_ID('dbo.Parts','U') IS NULL
                BEGIN
                    CREATE TABLE dbo.Parts
                    (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        AssemblyId INT NOT NULL,
                        Name NVARCHAR(120) NOT NULL,
                        PartNumber NVARCHAR(50) NULL,
                        Material NVARCHAR(60) NULL,
                        Quantity INT NOT NULL DEFAULT 1,
                        ModelFileName NVARCHAR(260) NULL,
                        ModelData VARBINARY(MAX) NULL,
                        Notes NVARCHAR(300) NULL,
                        CONSTRAINT FK_Parts_Assemblies FOREIGN KEY (AssemblyId) REFERENCES dbo.Assemblies(Id) ON DELETE CASCADE
                    );
                END
                ELSE
                BEGIN
                    IF COL_LENGTH('dbo.Parts','ModelPath') IS NOT NULL
                        ALTER TABLE dbo.Parts DROP COLUMN ModelPath;
                    IF COL_LENGTH('dbo.Parts','ModelFileName') IS NULL
                        ALTER TABLE dbo.Parts ADD ModelFileName NVARCHAR(260) NULL;
                    IF COL_LENGTH('dbo.Parts','ModelData') IS NULL
                        ALTER TABLE dbo.Parts ADD ModelData VARBINARY(MAX) NULL;
                END";

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = createAssemblies;
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = createParts;
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<AssemblyRecord>> GetAssembliesAsync()
        {
            var assemblies = new List<AssemblyRecord>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Id, Name, Code, Description, ModelFileName, CreatedAt, UpdatedAt
                    FROM dbo.Assemblies
                    ORDER BY Name;";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    assemblies.Add(new AssemblyRecord
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Code = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                        ModelFileName = reader.IsDBNull(4) ? null : reader.GetString(4),
                        CreatedAt = reader.GetDateTime(5),
                        UpdatedAt = reader.GetDateTime(6),
                        Parts = new List<PartRecord>()
                    });
                }
            }

            if (assemblies.Count == 0)
            {
                return assemblies;
            }

            var partsByAssembly = assemblies.ToDictionary(a => a.Id, _ => new List<PartRecord>());
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Id, AssemblyId, Name, PartNumber, Material, Quantity, ModelFileName, Notes
                    FROM dbo.Parts
                    ORDER BY PartNumber;";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var assemblyId = reader.GetInt32(1);
                    if (!partsByAssembly.TryGetValue(assemblyId, out var list))
                    {
                        continue;
                    }

                    list.Add(new PartRecord
                    {
                        Id = reader.GetInt32(0),
                        AssemblyId = assemblyId,
                        Name = reader.GetString(2),
                        PartNumber = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Material = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Quantity = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                        ModelFileName = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Notes = reader.IsDBNull(7) ? null : reader.GetString(7)
                    });
                }
            }

            foreach (var assembly in assemblies)
            {
                assembly.Parts = partsByAssembly[assembly.Id];
            }

            return assemblies;
        }

        public async Task<AssemblyRecord?> GetAssemblyAsync(int id)
        {
            var assemblies = await GetAssembliesAsync();
            return assemblies.FirstOrDefault(a => a.Id == id);
        }

        public async Task<(string FileName, byte[] Data)?> GetAssemblyModelAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT ModelFileName, ModelData FROM dbo.Assemblies WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (!await reader.ReadAsync() || reader.IsDBNull(1)) return null;
            var fileName = reader.IsDBNull(0) ? "model.a3d" : reader.GetString(0);
            var data = (byte[])reader.GetValue(1);
            return (fileName, data);
        }

        public async Task<(string FileName, byte[] Data)?> GetPartModelAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT ModelFileName, ModelData FROM dbo.Parts WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (!await reader.ReadAsync() || reader.IsDBNull(1)) return null;
            var fileName = reader.IsDBNull(0) ? "model.m3d" : reader.GetString(0);
            var data = (byte[])reader.GetValue(1);
            return (fileName, data);
        }

        public async Task<AssemblyRecord> SaveAssemblyAsync(AssemblyRecord assembly)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            if (assembly.Id == 0)
            {
                assembly.CreatedAt = DateTime.Now;
                assembly.UpdatedAt = DateTime.Now;
                using var insert = connection.CreateCommand();
                insert.CommandText = @"
                    INSERT INTO dbo.Assemblies (Name, Code, Description, ModelFileName, ModelData, CreatedAt, UpdatedAt)
                    OUTPUT INSERTED.Id
                    VALUES (@name, @code, @description, @modelFileName, @modelData, @createdAt, @updatedAt);";
                BindAssemblyParams(insert, assembly);
                insert.Parameters.AddWithValue("@createdAt", assembly.CreatedAt);
                insert.Parameters.AddWithValue("@updatedAt", assembly.UpdatedAt);

                var newId = (int)(await insert.ExecuteScalarAsync() ?? 0);
                assembly.Id = newId;
            }
            else
            {
                assembly.UpdatedAt = DateTime.Now;
                using var update = connection.CreateCommand();
                var hasData = assembly.ModelData != null;
                update.CommandText = hasData
                    ? @"UPDATE dbo.Assemblies
                        SET Name=@name, Code=@code, Description=@description,
                            ModelFileName=@modelFileName, ModelData=@modelData, UpdatedAt=@updatedAt
                        WHERE Id=@id;"
                    : @"UPDATE dbo.Assemblies
                        SET Name=@name, Code=@code, Description=@description,
                            ModelFileName=@modelFileName, UpdatedAt=@updatedAt
                        WHERE Id=@id;";
                BindAssemblyParams(update, assembly, skipModelData: !hasData);
                update.Parameters.AddWithValue("@updatedAt", assembly.UpdatedAt);
                update.Parameters.AddWithValue("@id", assembly.Id);

                await update.ExecuteNonQueryAsync();
            }

            var reloaded = await GetAssemblyAsync(assembly.Id);
            return reloaded ?? assembly;
        }

        public async Task DeleteAssemblyAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM dbo.Assemblies WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<PartRecord> SavePartAsync(int assemblyId, PartRecord part)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            if (part.Id == 0)
            {
                part.AssemblyId = assemblyId;
                using var insert = connection.CreateCommand();
                insert.CommandText = @"
                    INSERT INTO dbo.Parts (AssemblyId, Name, PartNumber, Material, Quantity, ModelFileName, ModelData, Notes)
                    OUTPUT INSERTED.Id
                    VALUES (@assemblyId, @name, @partNumber, @material, @quantity, @modelFileName, @modelData, @notes);";
                insert.Parameters.AddWithValue("@assemblyId", assemblyId);
                BindPartParams(insert, part);

                var newId = (int)(await insert.ExecuteScalarAsync() ?? 0);
                part.Id = newId;
            }
            else
            {
                using var update = connection.CreateCommand();
                var hasData = part.ModelData != null;
                update.CommandText = hasData
                    ? @"UPDATE dbo.Parts
                        SET Name=@name, PartNumber=@partNumber, Material=@material, Quantity=@quantity,
                            ModelFileName=@modelFileName, ModelData=@modelData, Notes=@notes
                        WHERE Id=@id;"
                    : @"UPDATE dbo.Parts
                        SET Name=@name, PartNumber=@partNumber, Material=@material, Quantity=@quantity,
                            ModelFileName=@modelFileName, Notes=@notes
                        WHERE Id=@id;";
                BindPartParams(update, part, skipModelData: !hasData);
                update.Parameters.AddWithValue("@id", part.Id);

                await update.ExecuteNonQueryAsync();
            }

            var reloaded = await GetPartAsync(part.Id);
            return reloaded ?? part;
        }

        public async Task DeletePartAsync(int partId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM dbo.Parts WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", partId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ReplaceAllAsync(IEnumerable<AssemblyRecord> assemblies)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM dbo.Parts; DELETE FROM dbo.Assemblies;";
                await delete.ExecuteNonQueryAsync();
            }

            foreach (var assembly in assemblies)
            {
                var savedAssembly = await InsertAssemblyInternalAsync(connection, transaction, assembly);
                foreach (var part in assembly.Parts)
                {
                    part.AssemblyId = savedAssembly.Id;
                    await InsertPartInternalAsync(connection, transaction, part);
                }
            }

            transaction.Commit();
        }

        private async Task<AssemblyRecord> InsertAssemblyInternalAsync(SqlConnection connection, SqlTransaction transaction, AssemblyRecord assembly)
        {
            assembly.CreatedAt = assembly.CreatedAt == default ? DateTime.Now : assembly.CreatedAt;
            assembly.UpdatedAt = DateTime.Now;

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT INTO dbo.Assemblies (Name, Code, Description, ModelFileName, ModelData, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.Id
                VALUES (@name, @code, @description, @modelFileName, @modelData, @createdAt, @updatedAt);";
            BindAssemblyParams(insert, assembly);
            insert.Parameters.AddWithValue("@createdAt", assembly.CreatedAt);
            insert.Parameters.AddWithValue("@updatedAt", assembly.UpdatedAt);

            var newId = (int)(await insert.ExecuteScalarAsync() ?? 0);
            assembly.Id = newId;
            return assembly;
        }

        private async Task InsertPartInternalAsync(SqlConnection connection, SqlTransaction transaction, PartRecord part)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT INTO dbo.Parts (AssemblyId, Name, PartNumber, Material, Quantity, ModelFileName, ModelData, Notes)
                VALUES (@assemblyId, @name, @partNumber, @material, @quantity, @modelFileName, @modelData, @notes);";
            insert.Parameters.AddWithValue("@assemblyId", part.AssemblyId);
            BindPartParams(insert, part);

            await insert.ExecuteNonQueryAsync();
        }

        private async Task<PartRecord?> GetPartAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, AssemblyId, Name, PartNumber, Material, Quantity, ModelFileName, Notes
                FROM dbo.Parts
                WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new PartRecord
            {
                Id = reader.GetInt32(0),
                AssemblyId = reader.GetInt32(1),
                Name = reader.GetString(2),
                PartNumber = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Material = reader.IsDBNull(4) ? null : reader.GetString(4),
                Quantity = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                ModelFileName = reader.IsDBNull(6) ? null : reader.GetString(6),
                Notes = reader.IsDBNull(7) ? null : reader.GetString(7)
            };
        }

        private static void BindAssemblyParams(SqlCommand cmd, AssemblyRecord assembly, bool skipModelData = false)
        {
            cmd.Parameters.AddWithValue("@name", assembly.Name);
            cmd.Parameters.AddWithValue("@code", (object?)NullIfEmpty(assembly.Code) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@description", (object?)assembly.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@modelFileName", (object?)assembly.ModelFileName ?? DBNull.Value);
            if (!skipModelData)
            {
                var p = cmd.Parameters.Add("@modelData", SqlDbType.VarBinary, -1);
                p.Value = (object?)assembly.ModelData ?? DBNull.Value;
            }
        }

        private static void BindPartParams(SqlCommand cmd, PartRecord part, bool skipModelData = false)
        {
            cmd.Parameters.AddWithValue("@name", part.Name);
            cmd.Parameters.AddWithValue("@partNumber", (object?)NullIfEmpty(part.PartNumber) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@material", (object?)part.Material ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@quantity", part.Quantity <= 0 ? 1 : part.Quantity);
            cmd.Parameters.AddWithValue("@modelFileName", (object?)part.ModelFileName ?? DBNull.Value);
            if (!skipModelData)
            {
                var p = cmd.Parameters.Add("@modelData", SqlDbType.VarBinary, -1);
                p.Value = (object?)part.ModelData ?? DBNull.Value;
            }
            cmd.Parameters.AddWithValue("@notes", (object?)part.Notes ?? DBNull.Value);
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
