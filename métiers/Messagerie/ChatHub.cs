// Fichier : Metier/Messagerie/ChatHub.cs
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Donnees;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Metier.Messagerie
{
    public class ChatHub : Hub
    {
        private readonly MessagerieService _messagerieService;
        private readonly ErpDbContext _dbContext;
        private readonly MRP.OrdreAchatService _oaService;
        private readonly IAService _iaService;

        // Dictionnaire global : userId -> liste de connectionIds
        // (un utilisateur peut avoir plusieurs onglets / navigateurs ouverts)
        private static readonly ConcurrentDictionary<int, ConcurrentBag<string>> _userConnections
            = new ConcurrentDictionary<int, ConcurrentBag<string>>();

        // Stockage temporaire (mémoire) du modèle choisi par conversation
        private static readonly ConcurrentDictionary<int, string> _conversationAiModels
            = new ConcurrentDictionary<int, string>();

        public ChatHub(MessagerieService messagerieService, ErpDbContext dbContext, MRP.OrdreAchatService oaService, IAService iaService)
        {
            _messagerieService = messagerieService;
            _dbContext = dbContext;
            _oaService = oaService;
            _iaService = iaService;
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

                    // Ajout au groupe personnel pour les notifications globales
                    await Groups.AddToGroupAsync(connId, $"user-{userId.Value}");
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
                // Si pas de pièce jointe : on persiste via le service en respectant le type
                if (string.IsNullOrEmpty(message.AttachmentUrl))
                {
                    var type = string.IsNullOrWhiteSpace(message.MessageType)
                        ? "text"
                        : message.MessageType;

                    var saved = await _messagerieService.SaveMessageAsync(
                        message.ConversationId,
                        message.SenderId,
                        message.Content,
                        type
                    );

                    await Clients.Group($"conv-{saved.ConversationId}")
                                 .SendAsync("ReceiveMessage", saved);

                    // --- NOTIFICATION GLOBALE ---
                    // Identifier le destinataire via le titre de la conversation directe
                    var notifConv = await _dbContext.Conversations.FindAsync(saved.ConversationId);
                    if (notifConv != null && notifConv.Type == "direct" && notifConv.Titre != null && notifConv.Titre.StartsWith("direct-"))
                    {
                        var idParts = notifConv.Titre.Replace("direct-", "").Split('-');
                        if (idParts.Length == 2 && int.TryParse(idParts[0], out int uid1) && int.TryParse(idParts[1], out int uid2))
                        {
                            var recipientId = (uid1 == saved.SenderId) ? uid2 : uid1;
                            await Clients.Group($"user-{recipientId}").SendAsync("ReceiveNotification", new
                            {
                                SenderId = saved.SenderId,
                                SenderName = saved.SenderName,
                                Content = saved.Content,
                                ConversationId = saved.ConversationId
                            });
                        }
                    }

                    // --- DETECTION DU CONTACT 'skyra-ia' ---
                    var conv = await _dbContext.Conversations.FindAsync(saved.ConversationId);
                    if (conv != null && conv.Type == "direct" && conv.Titre != null && conv.Titre.StartsWith("direct-"))
                    {
                        var parts = conv.Titre.Replace("direct-", "").Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int id1) && int.TryParse(parts[1], out int id2))
                        {
                            var otherUserId = (id1 == saved.SenderId) ? id2 : id1;
                            
                            var otherUser = await _dbContext.ErpUsers.FindAsync(otherUserId);
                            if (otherUser != null && (otherUser.Login == "skyra-ia" || otherUser.Login?.ToUpper() == "GEMINI"))
                            {
                                // Simuler la frappe dès le début
                                await SendTypingStatus(saved.ConversationId, otherUserId, true);

                                // 1. Créer la bulle immédiatement (mais invisible pour l'œil humain)
                                var iaSavedMsg = await _messagerieService.SaveMessageAsync(
                                    saved.ConversationId,
                                    otherUserId,
                                    "\u200B", // Invisible !
                                    "text"
                                );

                                // 2. Notifier les clients : La bulle est prête à recevoir le stream
                                await Clients.Group($"conv-{saved.ConversationId}")
                                             .SendAsync("ReceiveMessage", iaSavedMsg);

                                // 3. Lancer le stream


                                var fullResponse = new StringBuilder();
                                
                                // Récupérer le modèle choisi pour cette conversation (sinon null -> défaut)
                                _conversationAiModels.TryGetValue(saved.ConversationId, out var modelOverride);

                                await foreach (var chunk in _iaService.CallGeminiStreamAsync(saved.ConversationId, saved.Content, modelOverride))
                                {
                                    // Premier chunk reçu : on coupe l'animation de réflexion
                                    if (fullResponse.Length == 0) {
                                        await SendTypingStatus(saved.ConversationId, otherUserId, false);
                                    }

                                    fullResponse.Append(chunk);
                                    
                                    // Envoyer le morceau au client
                                    await Clients.Group($"conv-{saved.ConversationId}")
                                                 .SendAsync("ReceiveMessageChunk", iaSavedMsg.Id, chunk);
                                }

                                // 4. Sauvegarde finale en base de données
                                await _messagerieService.UpdateMessageHtmlAsync(iaSavedMsg.Id, fullResponse.ToString());

                                // Arrêt de la frappe (sécurité finale)
                                await SendTypingStatus(saved.ConversationId, otherUserId, false);
                            }
                        }
                    }
                    // ----------------------------------------
                }
                else
                {
                    // Pour les messages déjà créés côté Razor (image, file, audio, etc.)
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

        public async Task EditMessage(int messageId, int userId, string newContent)
        {
            try
            {
                var editedMessage = await _messagerieService.EditMessageTextAsync(messageId, userId, newContent);

                await Clients.Group($"conv-{editedMessage.ConversationId}")
                             .SendAsync("ReceiveMessageEdited", editedMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur ChatHub.EditMessage: {ex}");
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

        public async Task SendTypingStatus(int conversationId, int userId, bool isTyping)
        {
            bool isAi = false;
            try {
                var u = await _dbContext.ErpUsers.FindAsync(userId);
                if (u != null && u.Login?.ToUpper() == "GEMINI") isAi = true;
            } catch { }

            await Clients.Group($"conv-{conversationId}")
                         .SendAsync("UserTypingStatus", new { ConversationId = conversationId, UserId = userId, IsTyping = isTyping, IsAi = isAi });
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

        // --- FONCTION AJOUTÉE POUR SAUVEGARDER L'AFFICHAGE DES ORDRES D'ACHAT ---
        public async Task UpdateOaHtml(int messageId, string newHtmlContent)
        {
            try
            {
                var updatedMsg = await _messagerieService.UpdateMessageHtmlAsync(messageId, newHtmlContent);

                // --- DÉTECTION ACCEPTATION OA ---
                if (newHtmlContent != null && newHtmlContent.Contains("oa-status-acceptee"))
                {
                    // Extraction des données via regex sur le HTML
                    // Format attendu: data-plan-id="..." data-code="..." data-qty="..."
                    // On supporte " ou ' pour les attributs
                    var matchPlan = System.Text.RegularExpressions.Regex.Match(newHtmlContent, @"data-plan-id=[""'](\d+)[""']");
                    var matchCode = System.Text.RegularExpressions.Regex.Match(newHtmlContent, @"data-code=[""']([^""']+)[""']");
                    var matchQty = System.Text.RegularExpressions.Regex.Match(newHtmlContent, @"data-qty=[""']([\d\.,]+)[""']");

                    if (matchPlan.Success && matchCode.Success && matchQty.Success)
                    {
                        int planId = int.Parse(matchPlan.Groups[1].Value);
                        string code = matchCode.Groups[1].Value;
                        string qtyRaw = matchQty.Groups[1].Value.Replace(',', '.'); // standardiser le séparateur décimal
                        decimal qty = decimal.Parse(qtyRaw, System.Globalization.CultureInfo.InvariantCulture);

                        // Lancement de la génération du PDF OA
                        try
                        {
                            var fichierOA = await _oaService.GenererOrdreAchatAsync(planId, code, qty);
                            var ancrageOA = await _dbContext.BlockchainAncrages
                                .FirstOrDefaultAsync(a => a.RefDocument == fichierOA.ReferenceOF);

                            // Notifier la page MRPDetail pour qu'elle ajoute la ligne au tableau
                            await Clients.All.SendAsync("NouvelOrdreGenere", new
                            {
                                planId,
                                id            = fichierOA.Id,
                                codeArticle   = fichierOA.CodeArticle,
                                referenceOF   = fichierOA.ReferenceOF,
                                dateOrdre     = fichierOA.DateOrdre.ToString("dd/MM/yyyy HH:mm"),
                                type          = "OA",
                                statutBlockchain = ancrageOA?.Statut ?? "Local",
                                lienEtherscan = ancrageOA?.LienEtherscan ?? ""
                            });
                        }
                        catch (Exception exPdf)
                        {
                            Console.WriteLine($"[ERROR] Échec de génération PDF OA: {exPdf.Message}");
                        }
                    }
                }

                // Informe tous les gens dans la conversation que le HTML du message a été mis à jour
                await Clients.Group($"conv-{updatedMsg.ConversationId}")
                             .SendAsync("MessageUpdated", updatedMsg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur UpdateOaHtml: {ex}");
            }
        }
        // ==========================================
        // SIGNALISATION WEBRTC (APPELS AUDIO/VIDEO)
        // ==========================================

        public async Task InitiateCall(int targetUserId, int callerUserId, bool isVideo)
        {
            if (_userConnections.TryGetValue(targetUserId, out var connectionIds))
            {
                foreach (var cid in connectionIds)
                {
                    await Clients.Client(cid).SendAsync("ReceiveCall", new { CallerId = callerUserId, IsVideo = isVideo });
                }
            }
        }

        public async Task AcceptCall(int targetUserId, int responderUserId)
        {
            if (_userConnections.TryGetValue(targetUserId, out var connectionIds))
            {
                foreach (var cid in connectionIds)
                {
                    await Clients.Client(cid).SendAsync("CallAccepted", responderUserId);
                }
            }
        }

        public async Task RejectCall(int targetUserId, int responderUserId)
        {
            if (_userConnections.TryGetValue(targetUserId, out var connectionIds))
            {
                foreach (var cid in connectionIds)
                {
                    await Clients.Client(cid).SendAsync("CallRejected", responderUserId);
                }
            }
        }

        public async Task EndCall(int targetUserId)
        {
            if (_userConnections.TryGetValue(targetUserId, out var connectionIds))
            {
                foreach (var cid in connectionIds)
                {
                    await Clients.Client(cid).SendAsync("CallEnded");
                }
            }
        }

        public async Task SendWebRTCSignal(int targetUserId, string signalData)
        {
            if (_userConnections.TryGetValue(targetUserId, out var connectionIds))
            {
                foreach (var cid in connectionIds)
                {
                    await Clients.Client(cid).SendAsync("ReceiveWebRTCSignal", signalData);
                }
            }
        }

        // --- GESTION DES MODÈLES IA ---
        public async Task<List<string>> GetAvailableAiModels()
        {
            return await _iaService.GetAvailableChatModelsAsync();
        }

        public Task SetConversationAiModel(int conversationId, string modelName)
        {
            if (conversationId > 0 && !string.IsNullOrWhiteSpace(modelName))
            {
                _conversationAiModels[conversationId] = modelName;
            }
            return Task.CompletedTask;
        }
    }
}
