using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Donnees;
using Microsoft.EntityFrameworkCore;

namespace Metier
{
    public class IAService
    {
        private readonly HttpClient _httpClient;
        private readonly ErpDbContext _dbContext;

        // Cache statique pour éviter de solliciter l'API de listing à chaque message
        private static List<string>? _cachedModels = null;
        private static DateTime _lastDiscovery = DateTime.MinValue;

        public IAService(HttpClient httpClient, ErpDbContext dbContext)
        {
            _httpClient = httpClient;
            _dbContext = dbContext;
        }

        private async Task<List<string>> DiscoverModelsAsync(string apiKey)
        {
            // Rafraichir toutes les 24h ou si vide
            if (_cachedModels != null && (DateTime.Now - _lastDiscovery).TotalHours < 24)
                return _cachedModels;

            Console.WriteLine("[IA SYSTEM] Découverte des modèles Gemini disponibles...");
            try {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<string>();

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var models = new List<string>();
                
                if (doc.RootElement.TryGetProperty("models", out var modelList)) {
                    foreach (var m in modelList.EnumerateArray()) {
                        var name = m.GetProperty("name").GetString()?.Replace("models/", "");
                        var displayName = m.GetProperty("displayName").GetString()?.ToLower() ?? "";
                        var methods = m.GetProperty("supportedGenerationMethods").ToString();
                        
                        // Filtrage strict : on veut du texte, du chat, et surtout pas de technique pure
                        bool isTextModel = methods.Contains("generateContent");
                        bool isTechnical = name.Contains("robotics") || name.Contains("embedding") || name.Contains("vision") || name.Contains("aqa");
                        
                        if (name != null && isTextModel && !isTechnical) {
                            models.Add(name);
                        }
                    }
                }

                _cachedModels = models
                    .OrderByDescending(m => m.Contains("flash")) // Priorité Flash (Vitesse)
                    .ThenByDescending(m => m.Contains("pro"))    // Puis Pro (Intelligence)
                    .ToList();
                
                _lastDiscovery = DateTime.Now;
                Console.WriteLine($"[IA SYSTEM] {models.Count} modèles de chat validés : {string.Join(", ", _cachedModels)}");
                return _cachedModels;
            } catch (Exception ex) {
                Console.WriteLine($"[IA SYSTEM] Erreur lors de la découverte : {ex.Message}");
                return _cachedModels ?? new List<string>();
            }
        }

        public async Task<List<string>> GetAvailableChatModelsAsync()
        {
            IaConfiguration? config = null;
            try {
                config = await _dbContext.IaConfigurations.FirstOrDefaultAsync(c => c.IsEnabled);
            } catch { }
            
            if (config == null || string.IsNullOrWhiteSpace(config.ApiKey)) return new List<string>();
            return await DiscoverModelsAsync(config.ApiKey.Trim());
        }


        public async Task<string> CallGeminiAsync(int conversationId, string userMessage, string? modelOverride = null)
        {
            var sb = new StringBuilder();
            await foreach (var chunk in CallGeminiStreamAsync(conversationId, userMessage, modelOverride))
            {
                sb.Append(chunk);
            }
            return sb.ToString();
        }

        public async IAsyncEnumerable<string> CallGeminiStreamAsync(int conversationId, string userMessage, string? modelOverride = null)
        {
            IaConfiguration? config = null;
            bool dbError = false;
            try {
                config = await _dbContext.IaConfigurations.FirstOrDefaultAsync(c => c.IsEnabled);
            } catch {
                dbError = true;
            }

            if (dbError) {
                yield return "Erreur de connexion à la base de données.";
                yield break;
            }

            if (config == null) {
                yield return "Désolé, le module IA est désactivé.";
                yield break;
            }

            if (string.IsNullOrWhiteSpace(config.ApiKey)) {
                yield return "Veuillez configurer la clé API Gemini.";
                yield break;
            }

            var apiKey = config.ApiKey.Trim();
            
            var modelName = !string.IsNullOrWhiteSpace(modelOverride) 
                ? modelOverride.Trim() 
                : (config.ModelName ?? "gemini-1.5-flash-latest").Trim();

            if (modelName == "gemini-1.5-flash") modelName = "gemini-1.5-flash-latest";

            // --- RAG / CONTEXTE ---
            int countProduits = 0, countUsers = 0, countVehicules = 0;
            string nomsProduits = "";
            try {
                countProduits = await _dbContext.Produits.CountAsync();
                countUsers = await _dbContext.ErpUsers.CountAsync();
                countVehicules = await _dbContext.LogistiqueVehicules.CountAsync();
                var topProduits = await _dbContext.Produits.OrderByDescending(p => p.Id).Take(5).Select(p => p.Nom).ToListAsync();
                nomsProduits = string.Join(", ", topProduits);
            } catch { }

            string systemPromptText = string.IsNullOrWhiteSpace(config.SystemPrompt) 
                ? "Tu es GEMINI, le brillant assistant virtuel de l'ERP SKYRA." 
                : config.SystemPrompt;

            string docContext = @"
GUIDE DE CONFIGURATION ERP (DOCUMENTATION INTERNE) :
- Modules ACTIFS : 'Accueil' (/Home), 'Messagerie' (/Messagerie), 'Production/MRP' (/Fabrication), 'Logistique' (/Logistique/Index).
- Modules EN COURS DE DÉVELOPPEMENT (indisponibles) : Ventes, Achats, Stock, RH, Comptabilité.
- CRÉATION DE COMPTE : Il n'y a pas de module RH. Pour ajouter un nouvel employé, il faut se rendre sur la page '/CreateProfile'.
- STATISTIQUES : Employés: " + countUsers + @", Produits: " + countProduits + @", Véhicules: " + countVehicules;

            string contextualInstruction = systemPromptText + "\n\n" + docContext;

            var history = await _dbContext.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.Timestamp)
                .Take(10)
                .ToListAsync();
            history.Reverse();

            var contentsList = new List<object>();
            string lastRoleAdded = "";

            foreach (var msg in history)
            {
                var txt = msg.Content?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(txt) || txt == userMessage || txt == "\u200B") continue;
                string role = (msg.SenderId == 0) ? "model" : "user";
                if (role == lastRoleAdded) continue;
                contentsList.Add(new { role = role, parts = new[] { new { text = txt } } });
                lastRoleAdded = role;
            }

            if (lastRoleAdded == "user" && contentsList.Count > 0) contentsList.RemoveAt(contentsList.Count - 1);
            contentsList.Add(new { role = "user", parts = new[] { new { text = userMessage } } });
            if (contentsList.Count > 0 && contentsList[0].ToString().Contains("role = model")) contentsList.RemoveAt(0);

            var discovered = await DiscoverModelsAsync(apiKey);
            var modelsToTry = new List<string> { modelName };
            modelsToTry.AddRange(discovered);
            modelsToTry.Add("gemini-1.5-flash");

            var uniqueModels = new List<string>();
            foreach (var m in modelsToTry) {
                var clean = m.Replace("-latest", "").Trim();
                if (!uniqueModels.Contains(clean)) uniqueModels.Add(clean);
            }

            bool hasAtLeastOneResponse = false;
            foreach (var currentModel in uniqueModels)
            {
                var isLegacy = currentModel.Contains("1.0-pro");
                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{currentModel}:streamGenerateContent?key={apiKey}&alt=sse";

                object finalPayload;
                if (isLegacy) {
                    var legacyContents = new List<object>();
                    legacyContents.Add(new { role = "user", parts = new[] { new { text = "INSTRUCTIONS :\n" + contextualInstruction } } });
                    legacyContents.Add(new { role = "model", parts = new[] { new { text = "D'accord." } } });
                    legacyContents.AddRange(contentsList);
                    finalPayload = new { contents = legacyContents };
                } else {
                    finalPayload = new { system_instruction = new { parts = new[] { new { text = contextualInstruction } } }, contents = contentsList };
                }

                var json = JsonSerializer.Serialize(finalPayload);
                using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                if (resp.IsSuccessStatusCode) {
                    await foreach (var chunk in ParseStream(resp)) {
                        if (!string.IsNullOrEmpty(chunk)) {
                            hasAtLeastOneResponse = true;
                            yield return chunk;
                        }
                    }
                    if (hasAtLeastOneResponse) yield break;
                }

                if ((int)resp.StatusCode != 404 && (int)resp.StatusCode != 503 && (int)resp.StatusCode != 429) {
                    continue;
                }
            }

            if (!hasAtLeastOneResponse)
                yield return "Désolé, l'IA n'a pas pu générer de réponse (Modèles indisponibles ou format incompatible).";
        }

        private async IAsyncEnumerable<string> ParseStream(HttpResponseMessage response)
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var cleanLine = line.Trim();
                
                // En mode SSE, les données utiles commencent par 'data: '
                if (cleanLine.StartsWith("data:")) 
                {
                    cleanLine = cleanLine.Substring(5).Trim();
                } 
                else 
                {
                    continue; // On ignore les autres lignes (événements vides, etc.)
                }

                // Google envoie parfois [DONE] à la fin
                if (cleanLine == "[DONE]") continue;

                string? textFound = null;
                try {
                    using var doc = JsonDocument.Parse(cleanLine);
                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var candidate = candidates[0];
                        if (candidate.TryGetProperty("content", out var content) &&
                            content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                        {
                            textFound = parts[0].GetProperty("text").GetString();
                        }
                    }
                } catch { 
                    // Si un fragment n'est pas du JSON valide, on l'ignore silencieusement
                }

                if (!string.IsNullOrEmpty(textFound)) yield return textFound;
            }
        }
    }
}
