using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Donnees
{
    [Table("IaConfiguration")]
    public class IaConfiguration
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Provider { get; set; } = "Gemini";

        public string? ApiKey { get; set; }

        public string? SystemPrompt { get; set; }

        public string ModelName { get; set; } = "gemini-1.5-flash";

        public bool IsEnabled { get; set; } = true;

        public DateTime DateDerniereModification { get; set; } = DateTime.UtcNow;
    }
}
