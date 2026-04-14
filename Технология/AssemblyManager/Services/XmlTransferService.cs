using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using AssemblyManager.Models;

namespace AssemblyManager.Services
{
    public class XmlTransferService
    {
        private readonly XmlSerializer _serializer = new(typeof(AssemblyExport));

        public void Export(string filePath, IEnumerable<AssemblyRecord> assemblies)
        {
            var payload = new AssemblyExport
            {
                Assemblies = assemblies.Select(a => new AssemblyExportItem
                {
                    Name = a.Name,
                    Code = a.Code,
                    Description = a.Description,
                    Parts = a.Parts.Select(p => new PartExportItem
                    {
                        Name = p.Name,
                        PartNumber = p.PartNumber,
                        Material = p.Material,
                        Quantity = p.Quantity,
                        ModelFileName = p.ModelFileName,
                        Notes = p.Notes
                    }).ToList()
                }).ToList()
            };

            using var stream = File.Create(filePath);
            _serializer.Serialize(stream, payload);
        }

        public List<AssemblyRecord> Import(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            if (_serializer.Deserialize(stream) is not AssemblyExport export)
            {
                return new List<AssemblyRecord>();
            }

            var now = DateTime.Now;
            return export.Assemblies.Select(a => new AssemblyRecord
            {
                Name = a.Name,
                Code = a.Code,
                Description = a.Description,
                CreatedAt = now,
                UpdatedAt = now,
                Parts = a.Parts.Select(p => new PartRecord
                {
                    Name = p.Name,
                    PartNumber = p.PartNumber,
                    Material = p.Material,
                    Quantity = p.Quantity,
                    ModelFileName = p.ModelFileName,
                    Notes = p.Notes
                }).ToList()
            }).ToList();
        }
    }

    [XmlRoot("assemblies")]
    public class AssemblyExport
    {
        [XmlElement("assembly")]
        public List<AssemblyExportItem> Assemblies { get; set; } = new();
    }

    public class AssemblyExportItem
    {
        [XmlElement("name")]
        public string Name { get; set; } = string.Empty;

        [XmlElement("code")]
        public string Code { get; set; } = string.Empty;

        [XmlElement("description")]
        public string? Description { get; set; }

        [XmlArray("parts")]
        [XmlArrayItem("part")]
        public List<PartExportItem> Parts { get; set; } = new();
    }

    public class PartExportItem
    {
        [XmlElement("name")]
        public string Name { get; set; } = string.Empty;

        [XmlElement("number")]
        public string PartNumber { get; set; } = string.Empty;

        [XmlElement("material")]
        public string? Material { get; set; }

        [XmlElement("quantity")]
        public int Quantity { get; set; } = 1;

        [XmlElement("modelFileName")]
        public string? ModelFileName { get; set; }

        [XmlElement("notes")]
        public string? Notes { get; set; }
    }
}
