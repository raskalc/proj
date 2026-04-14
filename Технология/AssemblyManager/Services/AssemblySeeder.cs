using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AssemblyManager.Models;

namespace AssemblyManager.Services
{
    public static class AssemblySeeder
    {
        private static readonly string[] SearchRoots =
        {
            @"C:\Users\raskalc\RiderProjects\proj\СБОРКИ"
        };

        public static async Task SeedStamp12IfMissingAsync(AssemblyRepository repository)
        {
            var existing = await repository.GetAssembliesAsync();
            if (existing.Any(a => a.Code == "12")) return;

            var folder = FindAssemblyFolder("12");
            if (folder == null) return;

            var kompasDir = Path.Combine(folder, "КОМПАС");
            if (!Directory.Exists(kompasDir)) return;

            var a3d = Directory.EnumerateFiles(kompasDir, "*.a3d").FirstOrDefault();
            var assembly = new AssemblyRecord
            {
                Name = "Штамп для изготовления фанерных решеток",
                Code = "12",
                Description = "Сборка загружена автоматически из папки КОМПАС."
            };
            if (a3d != null)
            {
                assembly.ModelFileName = Path.GetFileName(a3d);
                assembly.ModelData = File.ReadAllBytes(a3d);
            }

            var saved = await repository.SaveAssemblyAsync(assembly);

            foreach (var file in Directory.EnumerateFiles(kompasDir, "*.m3d"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var (name, number) = SplitNameAndNumber(fileName);
                var part = new PartRecord
                {
                    Name = name,
                    PartNumber = number,
                    Quantity = 1,
                    ModelFileName = Path.GetFileName(file),
                    ModelData = File.ReadAllBytes(file)
                };
                await repository.SavePartAsync(saved.Id, part);
            }
        }

        private static string? FindAssemblyFolder(string code)
        {
            var prefix = code + "-";
            foreach (var root in SearchRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    var name = Path.GetFileName(dir);
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return dir;
                    }
                }
            }
            return null;
        }

        private static (string Name, string Number) SplitNameAndNumber(string fileName)
        {
            var match = Regex.Match(fileName, @"^(?<name>.+?)[_\s]+(?<num>\d+-\d+[\w-]*)$");
            if (match.Success)
            {
                return (match.Groups["name"].Value.Trim(), match.Groups["num"].Value.Trim());
            }
            return (fileName, string.Empty);
        }
    }
}
