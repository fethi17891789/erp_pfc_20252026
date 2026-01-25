// Fichier : Metier/Messagerie/ChatMessageDto.cs
using System;

namespace Metier.Messagerie
{
    /// <summary>
    /// DTO utilisé à la fois côté Hub SignalR et côté Service de messagerie.
    /// </summary>
    public class ChatMessageDto
    {
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        // NOUVEAU : type de message ("text", "audio", plus tard "file", etc.)
        public string MessageType { get; set; } = "text";

        // NOUVEAU : URL d'une éventuelle pièce jointe (audio, fichier…)
        public string? AttachmentUrl { get; set; }
    }
}
