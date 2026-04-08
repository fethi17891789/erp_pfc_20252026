using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Donnees;
using Microsoft.EntityFrameworkCore;

namespace Metier
{
    public class IAService
    {
        private readonly HttpClient _httpClient;
        private readonly ErpDbContext _dbContext;

        public IAService(HttpClient httpClient, ErpDbContext dbContext)
        {
            _httpClient = httpClient;
            _dbContext = dbContext;
        }

        public async Task<string> CallGeminiAsync(int conversationId, string userMessage)
        {
            try
            {
                // Vérifier si le module est actif en base
                var config = await _dbContext.IaConfigurations.FirstOrDefaultAsync(c => c.IsEnabled);
                
                if (config == null)
                    return "Désolé, le module IA est désactivé.";

                if (string.IsNullOrWhiteSpace(config.ApiKey))
                    return "Veuillez configurer la clé API Gemini dans la base de données (table IaConfiguration).";

                var apiKey = config.ApiKey?.Trim();
                var modelName = (config.ModelName ?? "gemini-1.5-flash-latest").Trim();
                
                // Forcer le nom correct
                if (modelName == "gemini-1.5-flash") modelName = "gemini-1.5-flash-latest";
                
                // --- 1. GÉNÉRATION DU RAG / STATISTIQUES EN TEMPS RÉEL ---
                int countProduits = await _dbContext.Produits.CountAsync();
                int countUsers = await _dbContext.ErpUsers.CountAsync();
                int countVehicules = await _dbContext.LogistiqueVehicules.CountAsync();
                var topProduitsList = await _dbContext.Produits.OrderByDescending(p => p.Id).Take(5).Select(p => p.Nom).ToListAsync();
                string nomsProduits = string.Join(", ", topProduitsList);

                string defaultPrompt = "Tu es GEMINI, le brillant assistant virtuel de l'ERP SKYRA. Ton rôle est d'aider les employés à gérer l'entreprise du bout des doigts.";
                string systemPromptText = string.IsNullOrWhiteSpace(config.SystemPrompt) ? defaultPrompt : config.SystemPrompt;

                string contextualInstruction = $@"{systemPromptText}

INFORMATIONS TEMPS RÉEL (BASE DE DONNÉES ERP) :
- Heure du Serveur : {DateTime.Now:F}
- Nombre total d'employés inscrits : {countUsers}
- Nombre de produits au catalogue : {countProduits}
- Les 5 derniers produits ajoutés sont : {nomsProduits}
- Nombre de véhicules logistiques : {countVehicules}

Utilise ces données uniquement si la question de l'utilisateur y fait référence. Sois toujours professionnel.";

                // --- 2. HISTORIQUE DE CONVERSATION ---
                var historyMessages = await _dbContext.Messages
                    .Where(m => m.ConversationId == conversationId)
                    .OrderByDescending(m => m.Timestamp)
                    .Take(10)
                    .ToListAsync();
                
                historyMessages.Reverse(); // Remettre dans l'ordre chronologique

                var contentsList = new System.Collections.Generic.List<object>();
                
                // On peuple l'historique
                foreach(var msg in historyMessages)
                {
                    // Ne pas s'inclure soi-même tout de suite
                    if(msg.Content == userMessage) continue;

                    var isHuman = msg.SenderId != 0; // Astuce simplifiée pour le test : si on trouve le sender on vérifie
                    var senderUser = await _dbContext.ErpUsers.FindAsync(msg.SenderId);
                    bool isIa = false;
                    if(senderUser != null && (senderUser.Login == "skyra-ia" || senderUser.Login?.ToUpper() == "GEMINI")) {
                        isIa = true;
                    }

                    contentsList.Add(new {
                        role = isIa ? "model" : "user",
                        parts = new[] { new { text = msg.Content } }
                    });
                }

                // Message actuel
                contentsList.Add(new {
                    role = "user",
                    parts = new[] { new { text = userMessage } }
                });

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

                // --- 3. PAYLOAD FINAL AVEC SYSTEM INTRUCTION ---
                var payload = new
                {
                    system_instruction = new
                    {
                        parts = new[] { new { text = contextualInstruction } }
                    },
                    contents = contentsList
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Appel HTTP à Google
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    return $"Erreur API ({response.StatusCode}) : {errorBody}";
                }

                // Parsing du retour JSON
                var responseBody = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var resContent) &&
                        resContent.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                    {
                         return parts[0].GetProperty("text").GetString() ?? "Réponse vide de l'IA.";
                    }
                }

                return "Format de réponse inattendu de la part de l'IA.";
            }
            catch (Exception ex)
            {
                return $"Erreur interne du service IA : {ex.Message}";
            }
        }
    }
}
