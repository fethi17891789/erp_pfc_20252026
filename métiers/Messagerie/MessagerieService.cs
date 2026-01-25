// Fichier : Metier/Messagerie/MessagerieService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using Donnees;

namespace Metier.Messagerie
{
    public class ConversationDto
    {
        public int ConversationId { get; set; }
        public int OtherUserId { get; set; }
        public string OtherUserName { get; set; } = string.Empty;
        public List<ChatMessageDto> Messages { get; set; } = new();
    }

    public class MessagerieService
    {
        private readonly DynamicConnectionProvider _connectionProvider;

        public MessagerieService(DynamicConnectionProvider connectionProvider)
        {
            _connectionProvider = connectionProvider;
        }

        private string GetConnectionString()
        {
            var conn = _connectionProvider.CurrentConnectionString;
            if (string.IsNullOrWhiteSpace(conn))
                throw new InvalidOperationException("ConnectionString vide dans DynamicConnectionProvider.");
            return conn;
        }

        /// <summary>
        /// Crée ou récupère une conversation DIRECTE entre deux utilisateurs,
        /// puis retourne l'historique (N derniers messages).
        /// </summary>
        public async Task<ConversationDto> GetOrCreateDirectConversationAsync(
            int currentUserId,
            int otherUserId,
            int maxMessages = 50)
        {
            if (currentUserId <= 0) throw new ArgumentException("currentUserId invalide");
            if (otherUserId <= 0) throw new ArgumentException("otherUserId invalide");
            if (currentUserId == otherUserId) throw new ArgumentException("Un utilisateur ne peut pas discuter avec lui-même.");

            var connString = GetConnectionString();
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            // 1) Récupérer les infos des deux utilisateurs (au moins le nom)
            var users = new Dictionary<int, (string Login, string Email)>();
            const string userSql = @"
                SELECT ""Id"", ""Login"", ""Email""
                FROM ""ErpUsers""
                WHERE ""Id"" = @id1 OR ""Id"" = @id2;";

            await using (var userCmd = new NpgsqlCommand(userSql, conn))
            {
                userCmd.Parameters.AddWithValue("id1", currentUserId);
                userCmd.Parameters.AddWithValue("id2", otherUserId);

                await using var reader = await userCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var login = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    users[id] = (login, email);
                }
            }

            if (!users.ContainsKey(currentUserId) || !users.ContainsKey(otherUserId))
                throw new InvalidOperationException("Un des utilisateurs n'existe pas dans ErpUsers.");

            string GetUserName(int id)
            {
                var u = users[id];
                return string.IsNullOrWhiteSpace(u.Login)
                    ? (string.IsNullOrWhiteSpace(u.Email) ? $"User {id}" : u.Email)
                    : u.Login;
            }

            // 2) Chercher une conversation directe existante
            int conversationId;

            const string findConvSql = @"
                SELECT ""Id""
                FROM ""Conversations""
                WHERE ""Type"" = 'direct'
                  AND ""Titre"" = @titre;";

            // Titre technique unique pour la paire (min,max)
            var minId = Math.Min(currentUserId, otherUserId);
            var maxId = Math.Max(currentUserId, otherUserId);
            var convTitre = $"direct-{minId}-{maxId}";

            await using (var findCmd = new NpgsqlCommand(findConvSql, conn))
            {
                findCmd.Parameters.AddWithValue("titre", convTitre);
                var obj = await findCmd.ExecuteScalarAsync();
                conversationId = obj is int i ? i : 0;
            }

            // 3) Si pas trouvée, créer
            if (conversationId == 0)
            {
                // On force IsArchived = FALSE pour respecter la contrainte NOT NULL
                const string insertConvSql = @"
                    INSERT INTO ""Conversations"" (""Titre"", ""Type"", ""CreatedByUserId"", ""IsArchived"")
                    VALUES (@titre, 'direct', @uid, FALSE)
                    RETURNING ""Id"";";

                await using var insertCmd = new NpgsqlCommand(insertConvSql, conn);
                insertCmd.Parameters.AddWithValue("titre", convTitre);
                insertCmd.Parameters.AddWithValue("uid", currentUserId);

                var newIdObj = await insertCmd.ExecuteScalarAsync();
                conversationId = (int)(newIdObj ?? 0);
            }

            // 4) Charger les derniers messages (texte + audio)
            var messages = new List<ChatMessageDto>();

            const string getMsgSql = @"
                SELECT m.""Id"", m.""SenderId"", m.""Content"", m.""Timestamp"", m.""MessageType"",
                       a.""FileUrl""
                FROM ""Messages"" m
                LEFT JOIN ""MessageAttachments"" a ON a.""MessageId"" = m.""Id""
                WHERE m.""ConversationId"" = @convId
                ORDER BY m.""Timestamp"" DESC
                LIMIT @limit;";

            await using (var msgCmd = new NpgsqlCommand(getMsgSql, conn))
            {
                msgCmd.Parameters.AddWithValue("convId", conversationId);
                msgCmd.Parameters.AddWithValue("limit", maxMessages);

                await using var reader = await msgCmd.ExecuteReaderAsync();
                var temp = new List<ChatMessageDto>();

                while (await reader.ReadAsync())
                {
                    var messageId = reader.GetInt32(0);
                    var senderId = reader.GetInt32(1);
                    var content = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    var ts = reader.GetDateTime(3);
                    var messageType = reader.IsDBNull(4) ? "text" : reader.GetString(4);
                    var attachmentUrl = reader.IsDBNull(5) ? null : reader.GetString(5);

                    temp.Add(new ChatMessageDto
                    {
                        ConversationId = conversationId,
                        SenderId = senderId,
                        SenderName = GetUserName(senderId),
                        Content = content,
                        Timestamp = ts,
                        MessageType = messageType,
                        AttachmentUrl = attachmentUrl
                    });
                }

                // Inverser pour avoir ancien -> récent
                temp.Reverse();
                messages = temp;
            }

            return new ConversationDto
            {
                ConversationId = conversationId,
                OtherUserId = otherUserId,
                OtherUserName = GetUserName(otherUserId),
                Messages = messages
            };
        }

        /// <summary>
        /// Enregistre un message texte dans une conversation existante (retourne le DTO complet).
        /// </summary>
        public async Task<ChatMessageDto> SaveMessageAsync(int conversationId, int senderId, string content)
        {
            if (conversationId <= 0) throw new ArgumentException("conversationId invalide");
            if (senderId <= 0) throw new ArgumentException("senderId invalide");
            if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("content vide");

            var connString = GetConnectionString();
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            // Vérifier que la conversation existe
            const string checkConvSql = @"SELECT COUNT(1) FROM ""Conversations"" WHERE ""Id"" = @cid;";
            await using (var checkCmd = new NpgsqlCommand(checkConvSql, conn))
            {
                checkCmd.Parameters.AddWithValue("cid", conversationId);
                var countObj = await checkCmd.ExecuteScalarAsync();
                var count = (long)(countObj ?? 0);
                if (count == 0)
                    throw new InvalidOperationException("Conversation introuvable.");
            }

            // Récupérer le nom de l'utilisateur
            string senderName;
            const string userSql = @"
                SELECT ""Login"", ""Email""
                FROM ""ErpUsers""
                WHERE ""Id"" = @sid;";

            await using (var userCmd = new NpgsqlCommand(userSql, conn))
            {
                userCmd.Parameters.AddWithValue("sid", senderId);

                await using var reader = await userCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var login = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    var email = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    if (!string.IsNullOrWhiteSpace(login))
                        senderName = login;
                    else if (!string.IsNullOrWhiteSpace(email))
                        senderName = email;
                    else
                        senderName = $"User {senderId}";
                }
                else
                {
                    senderName = $"User {senderId}";
                }
            }

            // Insérer le message texte
            const string insertSql = @"
                INSERT INTO ""Messages""
                    (""ConversationId"", ""SenderId"", ""Content"", ""Timestamp"", ""MessageType"", ""IsEdited"", ""IsDeleted"")
                VALUES
                    (@convId, @senderId, @content, NOW(), 'text', FALSE, FALSE)
                RETURNING ""Timestamp"";";

            DateTime ts;
            await using (var cmd = new NpgsqlCommand(insertSql, conn))
            {
                cmd.Parameters.AddWithValue("convId", conversationId);
                cmd.Parameters.AddWithValue("senderId", senderId);
                cmd.Parameters.AddWithValue("content", content);

                var obj = await cmd.ExecuteScalarAsync();
                ts = (DateTime)(obj ?? DateTime.UtcNow);
            }

            return new ChatMessageDto
            {
                ConversationId = conversationId,
                SenderId = senderId,
                SenderName = senderName,
                Content = content,
                Timestamp = ts,
                MessageType = "text",
                AttachmentUrl = null
            };
        }

        /// <summary>
        /// Enregistre un message audio (MessageType = 'audio') avec une URL de fichier.
        /// </summary>
        public async Task<ChatMessageDto> SaveAudioMessageAsync(int conversationId, int senderId, string fileUrl)
        {
            if (conversationId <= 0) throw new ArgumentException("conversationId invalide");
            if (senderId <= 0) throw new ArgumentException("senderId invalide");
            if (string.IsNullOrWhiteSpace(fileUrl)) throw new ArgumentException("fileUrl vide");

            var connString = GetConnectionString();
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            // Vérifier que la conversation existe
            const string checkConvSql = @"SELECT COUNT(1) FROM ""Conversations"" WHERE ""Id"" = @cid;";
            await using (var checkCmd = new NpgsqlCommand(checkConvSql, conn))
            {
                checkCmd.Parameters.AddWithValue("cid", conversationId);
                var countObj = await checkCmd.ExecuteScalarAsync();
                var count = (long)(countObj ?? 0);
                if (count == 0)
                    throw new InvalidOperationException("Conversation introuvable.");
            }

            // Récupérer le nom de l'utilisateur
            string senderName;
            const string userSql = @"
                SELECT ""Login"", ""Email""
                FROM ""ErpUsers""
                WHERE ""Id"" = @sid;";

            await using (var userCmd = new NpgsqlCommand(userSql, conn))
            {
                userCmd.Parameters.AddWithValue("sid", senderId);

                await using var reader = await userCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var login = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    var email = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    if (!string.IsNullOrWhiteSpace(login))
                        senderName = login;
                    else if (!string.IsNullOrWhiteSpace(email))
                        senderName = email;
                    else
                        senderName = $"User {senderId}";
                }
                else
                {
                    senderName = $"User {senderId}";
                }
            }

            // 1) Insérer le message (type = audio, content vide ou texte explicatif)
            const string insertMsgSql = @"
                INSERT INTO ""Messages""
                    (""ConversationId"", ""SenderId"", ""Content"", ""Timestamp"", ""MessageType"", ""IsEdited"", ""IsDeleted"")
                VALUES
                    (@convId, @senderId, @content, NOW(), 'audio', FALSE, FALSE)
                RETURNING ""Id"", ""Timestamp"";";

            int messageId;
            DateTime ts;
            await using (var cmd = new NpgsqlCommand(insertMsgSql, conn))
            {
                cmd.Parameters.AddWithValue("convId", conversationId);
                cmd.Parameters.AddWithValue("senderId", senderId);
                cmd.Parameters.AddWithValue("content", string.Empty);

                await using var readerMsg = await cmd.ExecuteReaderAsync();
                if (await readerMsg.ReadAsync())
                {
                    messageId = readerMsg.GetInt32(0);
                    ts = readerMsg.GetDateTime(1);
                }
                else
                {
                    throw new InvalidOperationException("Échec d'insertion du message audio.");
                }
            }

            // 2) Insérer l'attachement dans MessageAttachments
            const string insertAttachSql = @"
                INSERT INTO ""MessageAttachments""
                    (""MessageId"", ""AttachmentType"", ""FileName"", ""FileUrl"", ""FileSizeBytes"")
                VALUES
                    (@mid, @type, @fname, @furl, NULL);";

            await using (var attachCmd = new NpgsqlCommand(insertAttachSql, conn))
            {
                attachCmd.Parameters.AddWithValue("mid", messageId);
                attachCmd.Parameters.AddWithValue("type", "audio");
                attachCmd.Parameters.AddWithValue("fname", System.IO.Path.GetFileName(fileUrl));
                attachCmd.Parameters.AddWithValue("furl", fileUrl);
                await attachCmd.ExecuteNonQueryAsync();
            }

            return new ChatMessageDto
            {
                ConversationId = conversationId,
                SenderId = senderId,
                SenderName = senderName,
                Content = "[message audio]",
                Timestamp = ts,
                MessageType = "audio",
                AttachmentUrl = fileUrl
            };
        }
    }
}
