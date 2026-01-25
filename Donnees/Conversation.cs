using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Donnees
{
    [Table("Conversations")]
    public class Conversation
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(200)]
        public string? Titre { get; set; }

        [Required, MaxLength(20)]
        public string Type { get; set; } = "direct"; // direct, groupe, support

        public int? CreatedByUserId { get; set; }

        public bool IsArchived { get; set; } = false;

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;

        public List<Message> Messages { get; set; } = new();
    }
}