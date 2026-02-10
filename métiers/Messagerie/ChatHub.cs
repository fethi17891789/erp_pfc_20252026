// Fichier : Metier/Messagerie/ChatHub.cs
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Donnees;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Metier.Messagerie
{
    public class ChatHub : Hub
    {
        private readonly MessagerieService _messagerieService;
        private readonly ErpDbContext _dbContext;

        // Dictionnaire global : userId -> liste de connectionIds
        // (un utilisateur peut avoir plusieurs onglets / navigateurs ouverts)
        private static readonly ConcurrentDictionary<int, ConcurrentBag<string>> _userConnections
            = new ConcurrentDictionary<int, ConcurrentBag<string>>();

        public ChatHub(MessagerieService messagerieService, ErpDbContext dbContext)
        {
            _messagerieService = messagerieService;
            _dbContext = dbContext;
        }

        /// <summary>
        /// On suppose que le client passe son userId en query string : /chathub?userId=123
        /// (on lira ça pour mapper ConnectionId -> userId).
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            try
            {
                var httpContext = Context.GetHttpContext();
                int? userId = null;

                if (httpContext != null && httpContext.Request.Query.ContainsKey("userId"))
                {
                    var raw = httpContext.Request.Query["userId"].ToString();
                    if (int.TryParse(raw, out var parsed))
                    {
                        userId = parsed;
                    }
                }

                if (userId.HasValue && userId.Value > 0)
                {
                    var connId = Context.ConnectionId;

                    var bag = _userConnections.GetOrAdd(userId.Value, _ => new ConcurrentBag<string>());
                    bag.Add(connId);

                    // Si c'est la première connexion de ce user => on le marque en ligne en BDD et on broadcast
                    if (bag.Count == 1)
                    {
                        await SetUserOnlineStatusAsync(userId.Value, true);

                        await Clients.All.SendAsync("UserOnline", new
                        {
                            UserId = userId.Value
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur ChatHub.OnConnectedAsync: {ex}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                int? userId = null;

                // On retrouve le userId en cherchant dans le dictionnaire
                foreach (var kvp in _userConnections)
                {
                    var uid = kvp.Key;
                    var bag = kvp.Value;

                    if (bag.Contains(Context.ConnectionId))
                    {
                        userId = uid;

                        var remaining = new ConcurrentBag<string>(bag.Where(c => c != Context.ConnectionId));

                        if (remaining.IsEmpty)
                        {
                            _userConnections.TryRemove(uid, out _);
                        }
                        else
                        {
                            _userConnections[uid] = remaining;
                        }
                        break;
                    }
                }

                if (userId.HasValue)
                {
                    // S'il n'a plus aucune connexion => hors ligne
                    if (!_userConnections.ContainsKey(userId.Value))
                    {
                        await SetUserOnlineStatusAsync(userId.Value, false);

                        await Clients.All.SendAsync("UserOffline", new
                        {
                            UserId = userId.Value
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur ChatHub.OnDisconnectedAsync: {ex}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task SetUserOnlineStatusAsync(int userId, bool isOnline)
        {
            try
            {
                var user = await _dbContext.ErpUsers.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null) return;

                user.IsOnline = isOnline;
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur SetUserOnlineStatusAsync: {ex}");
            }
        }

        // Permet au client, au besoin, de demander la liste des users "en ligne" en temps réel
        // (utile si tu veux resynchroniser après un refresh complet).
        public Task<int[]> GetOnlineUsers()
        {
            var onlineUserIds = _userConnections.Keys.ToArray();
            return Task.FromResult(onlineUserIds);
        }

        public async Task SendMessage(ChatMessageDto message)
        {
            try
            {
                // Si c'est un simple texte
                if (string.Equals(message.MessageType, "text", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrEmpty(message.AttachmentUrl))
                {
                    var saved = await _messagerieService.SaveMessageAsync(
                        message.ConversationId,
                        message.SenderId,
                        message.Content
                    );

                    await Clients.Group($"conv-{saved.ConversationId}")
                                 .SendAsync("ReceiveMessage", saved);
                }
                else
                {
                    // Pour les messages déjà créés côté Razor (image, file, etc.)
                    await Clients.Group($"conv-{message.ConversationId}")
                                 .SendAsync("ReceiveMessage", message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur ChatHub.SendMessage: {ex}");
                throw;
            }
        }

        // Notification pour un message audio déjà créé côté Razor
        public async Task SendAudioMessage(ChatMessageDto message)
        {
            try
            {
                await Clients.Group($"conv-{message.ConversationId}")
                             .SendAsync("ReceiveMessage", message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur ChatHub.SendAudioMessage: {ex}");
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

        // Marquer une conversation comme lue par un utilisateur
        public async Task MarkConversationAsRead(int conversationId, int readerUserId)
        {
            try
            {
                await _messagerieService.MarkMessagesAsReadAsync(conversationId, readerUserId);

                await Clients.Group($"conv-{conversationId}")
                             .SendAsync("ConversationRead", new
                             {
                                 ConversationId = conversationId,
                                 ReaderUserId = readerUserId
                             });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur ChatHub.MarkConversationAsRead: {ex}");
                throw;
            }
        }
    }
}
