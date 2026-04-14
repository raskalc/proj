using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace AssemblyManager.Models
{
    public class PartRecord
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string PartNumber { get; set; } = string.Empty;

        [MaxLength(60)]
        public string? Material { get; set; }

        public int Quantity { get; set; } = 1;

        [MaxLength(300)]
        public string? ModelPath { get; set; }

        [MaxLength(300)]
        public string? Notes { get; set; }

        public int AssemblyId { get; set; }

        [XmlIgnore]
        public AssemblyRecord? Assembly { get; set; }
    }
}
