using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Donnees
{
    [Table("Messages")]
    public class Message
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ConversationId { get; set; }

        [ForeignKey("ConversationId")]
        public Conversation? Conversation { get; set; }

        [Required]
        public int SenderId { get; set; }

        public string? Content { get; set; }

        [Required, MaxLength(20)]
        public string MessageType { get; set; } = "text";

        public bool IsEdited { get; set; } = false;

        public DateTime? EditedAt { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public List<MessageAttachment> Attachments { get; set; } = new();
    }
}