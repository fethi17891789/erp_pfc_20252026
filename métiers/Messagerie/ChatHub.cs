// Fichier : Metier/Messagerie/ChatHub.cs
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Metier.Messagerie
{
    public class ChatHub : Hub
    {
        private readonly MessagerieService _messagerieService;

        public ChatHub(MessagerieService messagerieService)
        {
            _messagerieService = messagerieService;
        }

        public async Task SendMessage(ChatMessageDto message)
        {
            try
            {
                var saved = await _messagerieService.SaveMessageAsync(
                    message.ConversationId,
                    message.SenderId,
                    message.Content
                );

                await Clients.Group($"conv-{saved.ConversationId}")
                             .SendAsync("ReceiveMessage", saved);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur ChatHub: {ex}");
                throw;
            }
        }

        // NOUVEAU : notification pour un message audio déjà créé côté Razor
        public async Task SendAudioMessage(ChatMessageDto message)
        {
            try
            {
                // Ici, on suppose que SaveAudioMessageAsync a déjà été appelé
                // dans le handler Razor UploadAudio. On push juste le DTO.
                await Clients.Group($"conv-{message.ConversationId}")
                             .SendAsync("ReceiveMessage", message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur ChatHub (audio): {ex}");
                throw;
            }
        }

        public async Task JoinConversation(int conversationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conv-{conversationId}");
        }

        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv-{conversationId}");
        }
    }
}
