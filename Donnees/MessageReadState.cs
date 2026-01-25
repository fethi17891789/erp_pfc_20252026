using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Donnees
{
    [Table("MessageReadStates")]
    public class MessageReadState
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MessageId { get; set; }

        [Required]
        public int UserId { get; set; }

        public DateTime ReadAt { get; set; } = DateTime.UtcNow;
    }
}