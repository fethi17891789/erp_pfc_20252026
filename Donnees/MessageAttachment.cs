using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Donnees
{
    [Table("MessageAttachments")]
    public class MessageAttachment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MessageId { get; set; }

        [ForeignKey("MessageId")]
        public Message? Message { get; set; }

        [Required, MaxLength(20)]
        public string AttachmentType { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string FileUrl { get; set; } = string.Empty;

        public long? FileSizeBytes { get; set; }
    }
}