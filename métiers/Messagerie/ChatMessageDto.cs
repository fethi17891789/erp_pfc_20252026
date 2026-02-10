// Fichier : Metier/Messagerie/ChatMessageDto.cs
using System;

namespace Metier.Messagerie
{
    /// <summary>
    /// DTO utilisé à la fois côté Hub SignalR et côté Service de messagerie.
    /// </summary>
    public class ChatMessageDto
    {
        public int Id { get; set; }                   // Id du message (Messages.Id)
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        // Type de message ("text", "audio", "image", "file", etc.)
        public string MessageType { get; set; } = "text";

        // URL d'une éventuelle pièce jointe (audio, fichier, image…)
        public string? AttachmentUrl { get; set; }

        // Optionnel : permet plus tard d'afficher "Vu" ou non
        public bool IsReadByOther { get; set; } = false;
    }
}
