using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AssemblyManager.Models
{
    public class AssemblyRecord
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public ICollection<PartRecord> Parts { get; set; } = new List<PartRecord>();
    }
}
