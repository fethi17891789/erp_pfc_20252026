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
            // Rafraîchir toutes les 4 heures (Optimisation Turbo)
            if (_cachedModels != null && (DateTime.Now - _lastDiscovery).TotalHours < 4)
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
                        
                        // Filtrage strict : on veut la famille GEMINI (vitesse/précision JSON)
                        bool isGemini = name.StartsWith("gemini", StringComparison.OrdinalIgnoreCase);
                        bool isTextModel = methods.Contains("generateContent");
                        bool isTechnical = name.Contains("robotics") || name.Contains("embedding") || name.Contains("vision") || name.Contains("aqa");
                        
                        if (name != null && isGemini && isTextModel && !isTechnical) {
                            models.Add(name);
                        }
                    }
                }

                // --- ALGORITHME DE CLASSEMENT "ELITE FULL SPECTRUM" ---
                // On trie par : Famille (Pro > Flash) puis Version (2.5 > 2.0 > 1.5)
                _cachedModels = models
                    .OrderByDescending(m => m.Contains("pro"))   // 1. Les cerveaux (Pro)
                    .ThenByDescending(m => m.Contains("2.5"))   // 2. Version futuriste (2.5)
                    .ThenByDescending(m => m.Contains("2.0"))   // 3. Version moderne (2.0)
                    .ThenByDescending(m => m.Contains("1.5"))   // 4. Version stable (1.5)
                    .ThenByDescending(m => m.Contains("flash")) // 5. Les rapides (Flash)
                    .ThenByDescending(m => m.Contains("latest"))// 6. Les alias "latest"
                    .ThenByDescending(m => m)                    // 7. Reste
                    .ToList();
                
                _lastDiscovery = DateTime.Now;
                Console.WriteLine($"[IA SYSTEM] Mode Combat activé. Hiérarchie Full Spectrum : {string.Join(" >> ", _cachedModels)}");
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

        /// <summary>
        /// Appelle Gemini pour obtenir une réponse structurée (JSON) et retourne aussi le modèle utilisé.
        /// </summary>
        public async Task<(string Json, string ModelUsed)> AskAiJsonAsync(string systemPrompt, string userPrompt)
        {
            IaConfiguration? config = null;
            try {
                config = await _dbContext.IaConfigurations.FirstOrDefaultAsync(c => c.IsEnabled);
            } catch { }

            if (config == null || string.IsNullOrWhiteSpace(config.ApiKey)) 
                return ("{\"error\": \"IA non configurée\"}", "N/A");

            var apiKey = config.ApiKey.Trim();
            var modelName = (config.ModelName ?? "gemini-1.5-flash-latest").Trim();
            if (modelName == "gemini-1.5-flash") modelName = "gemini-1.5-flash-latest";

            // Découverte des modèles pour le failover (Dynamique - Zéro Hardcode)
            var discovered = await DiscoverModelsAsync(apiKey);
            var modelsToTry = new List<string>();
            
            // On utilise l'ordre de puissance découvert
            modelsToTry.AddRange(discovered);
            
            // On s'assure que le modèle choisi par l'utilisateur en config passe en premier s'il est dispo
            if (discovered.Contains(modelName)) {
                modelsToTry.Remove(modelName);
                modelsToTry.Insert(0, modelName);
            }

            if (!modelsToTry.Any()) {
                // Fallback ultime de sécurité si la liste est vide (très rare)
                modelsToTry.Add("gemini-1.5-flash-latest");
            }

            foreach (var currentModel in modelsToTry)
            {
                var targetModel = currentModel.Contains("/") ? currentModel : $"models/{currentModel}";
                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/{targetModel}:generateContent?key={apiKey}";

                Console.WriteLine($"[CRM IA] Tentative avec le modèle : {targetModel}...");

                var payload = new {
                    system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                    contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
                    // On retire tools = google_search ici car c'est lui qui ralentit tout et sature les quotas
                    generationConfig = new { 
                        response_mime_type = "application/json",
                        temperature = 0.0
                    }
                };

                try {
                    var json = JsonSerializer.Serialize(payload);
                    using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                    using var resp = await _httpClient.SendAsync(req);

                    if (resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(body);
                        
                        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                        {
                            var first = candidates[0];
                            if (first.TryGetProperty("content", out var content) &&
                                content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                            {
                                var textFound = parts[0].GetProperty("text").GetString();
                                if (!string.IsNullOrEmpty(textFound)) {
                                    // Nettoyage des balises Markdown si présentes
                                    var cleanText = textFound.Trim();
                                    if (cleanText.StartsWith("```")) {
                                        var lines = cleanText.Split('\n').ToList();
                                        if (lines.First().Contains("json")) lines.RemoveAt(0);
                                        else lines.RemoveAt(0);
                                        if (lines.Last().Contains("```")) lines.RemoveAt(lines.Count - 1);
                                        cleanText = string.Join("\n", lines).Trim();
                                    }

                                    Console.WriteLine($"[CRM IA] Succès avec {targetModel}.");
                                    return (cleanText, targetModel);
                                }
                            }
                        }
                    }
                    else if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest || resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        var errBody = await resp.Content.ReadAsStringAsync();
                        Console.WriteLine($"[CRM IA] Modèle {targetModel} indisponible (Status: {resp.StatusCode}). Tentative suivante...");
                        
                        if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest && !errBody.Contains("404")) {
                             // Fallback : Si l'erreur est 400 (et pas un 404 masqué), on tente sans outils.
                             Console.WriteLine($"[CRM IA] Mode recherche refusé par {targetModel}. Retentative sans outils...");
                             var fallbackPayload = new {
                                 system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                                 contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
                                 generationConfig = new { response_mime_type = "application/json", temperature = 0.1 }
                             };
                             var fbJson = JsonSerializer.Serialize(fallbackPayload);
                             using var fbReq = new HttpRequestMessage(HttpMethod.Post, apiUrl) { Content = new StringContent(fbJson, Encoding.UTF8, "application/json") };
                             using var fbResp = await _httpClient.SendAsync(fbReq);
                             if (fbResp.IsSuccessStatusCode) {
                                  var fbBody = await fbResp.Content.ReadAsStringAsync();
                                  using var fbDoc = JsonDocument.Parse(fbBody);
                                  var fbText = fbDoc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                                  return (fbText ?? "{}", targetModel + " (No-Search)");
                             }
                        }
                    }
                    else {
                        var errBody = await resp.Content.ReadAsStringAsync();
                        Console.WriteLine($"[CRM IA] Échec modèle {targetModel} (HTTP {(int)resp.StatusCode}). Réponse: {errBody}");
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine($"[CRM IA] Erreur critique sur {currentModel} : {ex.Message}");
                }
            }

            return ("{\"error\": \"Tous les modèles Gemini ont échoué ou sont hors quota.\"}", "Erreur");
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
            var modelsToTry = new List<string>();
            
            // On privilégie le modèle choisi dans la config, sinon on suit la hiérarchie dynamique
            if (discovered.Contains(modelName)) modelsToTry.Add(modelName);
            foreach(var d in discovered) if(!modelsToTry.Contains(d)) modelsToTry.Add(d);

            if (!modelsToTry.Any()) modelsToTry.Add("gemini-1.5-flash-latest");

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
