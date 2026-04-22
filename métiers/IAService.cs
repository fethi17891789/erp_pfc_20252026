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
        public async Task<(string Json, string ModelUsed)> AskAiJsonAsync(string systemPrompt, string userPrompt, bool enableSearch = false)
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

                object payload;
                if (enableSearch) {
                    // IMPORTANT : response_mime_type="application/json" est incompatible avec googleSearch.
                    // On laisse Gemini répondre en texte libre mais on lui demande du JSON dans le prompt.
                    // Le nettoyage markdown existant gère le reste.
                    payload = new {
                        system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                        contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
                        tools = new[] { new { googleSearch = new object() } },
                        generationConfig = new {
                            temperature = 0.0
                        }
                    };
                } else {
                    payload = new {
                        system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                        contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
                        generationConfig = new {
                            response_mime_type = "application/json",
                            temperature = 0.0
                        }
                    };
                }

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

            // --- RAG / CONTEXTE DYNAMIQUE (données réelles depuis la BDD) ---
            int countProduits = 0, countUsers = 0, countVehicules = 0, countContacts = 0, countMRP = 0;
            var lignesProduits = new StringBuilder();
            var lignesVehicules = new StringBuilder();
            var lignesContacts = new StringBuilder();
            var lignesMRP = new StringBuilder();

            try {
                countProduits = await _dbContext.Produits.CountAsync();
                countUsers = await _dbContext.ErpUsers.CountAsync();
                countVehicules = await _dbContext.LogistiqueVehicules.CountAsync();
                countContacts = await _dbContext.Contacts.CountAsync();
                countMRP = await _dbContext.MRPPlans.CountAsync();

                // Produits avec tous les champs utiles
                var produits = await _dbContext.Produits
                    .OrderBy(p => p.Nom).Take(40)
                    .Select(p => new { p.Nom, p.Reference, p.TypeTechnique, p.Type, p.QuantiteDisponible, p.PrixVente, p.CoutTotal })
                    .ToListAsync();
                foreach (var p in produits) {
                    var typeTech = p.TypeTechnique switch {
                        Donnees.TypeTechniqueProduit.MatierePremiere => "Matière première",
                        Donnees.TypeTechniqueProduit.SemiFini       => "Semi-fini",
                        Donnees.TypeTechniqueProduit.Fini            => "Produit fini",
                        Donnees.TypeTechniqueProduit.SemiFiniEtFini  => "Semi-fini+Fini",
                        _ => "?"
                    };
                    lignesProduits.AppendLine($"  - {p.Nom} (Réf: {p.Reference}) | Type: {typeTech} / {p.Type} | Qté dispo: {p.QuantiteDisponible} | Prix vente: {p.PrixVente}€ | Coût: {p.CoutTotal}€");
                }

                // Véhicules avec détails carburant/statut
                var vehicules = await _dbContext.LogistiqueVehicules
                    .OrderBy(v => v.Nom)
                    .Select(v => new { v.Nom, v.Matricule, v.TypeTransport, v.TypeCarburant, v.Statut, v.Marque, v.Modele, v.Annee })
                    .ToListAsync();
                foreach (var v in vehicules) {
                    lignesVehicules.AppendLine($"  - {v.Nom} ({v.Marque} {v.Modele} {v.Annee}) | Matricule: {v.Matricule} | Transport: {v.TypeTransport} | Carburant: {v.TypeCarburant ?? "N/A"} | Statut: {v.Statut}");
                }

                // Contacts (clients, fournisseurs, partenaires...)
                var contacts = await _dbContext.Contacts
                    .OrderBy(c => c.FullName).Take(30)
                    .Select(c => new { c.FullName, c.Roles, c.Email })
                    .ToListAsync();
                foreach (var c in contacts) {
                    var roles = new List<string>();
                    if (c.Roles.HasFlag(Donnees.ContactRole.Client))      roles.Add("Client");
                    if (c.Roles.HasFlag(Donnees.ContactRole.Fournisseur)) roles.Add("Fournisseur");
                    if (c.Roles.HasFlag(Donnees.ContactRole.Employe))     roles.Add("Employé");
                    if (c.Roles.HasFlag(Donnees.ContactRole.Partenaire))  roles.Add("Partenaire");
                    if (c.Roles.HasFlag(Donnees.ContactRole.Investisseur))roles.Add("Investisseur");
                    lignesContacts.AppendLine($"  - {c.FullName} | Rôles: {(roles.Any() ? string.Join(", ", roles) : "Aucun")} | Email: {c.Email ?? "N/A"}");
                }

                // Plans MRP récents
                var mrpPlans = await _dbContext.MRPPlans
                    .OrderByDescending(m => m.DateCreation).Take(5)
                    .Select(m => new { m.Reference, m.Statut, m.TypePlan, m.DateDebutHorizon, m.DateFinHorizon })
                    .ToListAsync();
                foreach (var m in mrpPlans) {
                    lignesMRP.AppendLine($"  - {m.Reference} | Statut: {m.Statut} | Type: {m.TypePlan ?? "N/A"} | Horizon: {m.DateDebutHorizon:dd/MM/yyyy} → {m.DateFinHorizon:dd/MM/yyyy}");
                }
            } catch (Exception exRag) {
                Console.WriteLine($"[IA STREAM] Erreur chargement contexte ERP: {exRag.Message}");
            }

            string systemPromptText = string.IsNullOrWhiteSpace(config.SystemPrompt)
                ? "Tu es GEMINI, le brillant assistant virtuel de l'ERP SKYRA."
                : config.SystemPrompt;

            // --- GUIDE MÉTIER STATIQUE (logique de fonctionnement de l'ERP) ---
            const string guideMetier = @"
=== GUIDE INTERNE : FONCTIONNEMENT DE L'ERP SKYRA ===

[INSTRUCTION FONDAMENTALE]
Tu es un assistant intelligent, pas un simple moteur de recherche. Quand tu consultes les données de l'ERP, tu dois les INTERPRÉTER avec ta propre connaissance générale, pas seulement les lire. Si un utilisateur emploie un terme qui ne correspond pas exactement aux valeurs en base (ex: 'thermique' alors que la base contient 'Diesel'/'Essence', ou 'voiture verte' pour un hybride, ou 'grosse quantité' pour une valeur élevée), fais le lien toi-même grâce à ta connaissance du monde. Ne réponds jamais 'je n'ai pas cette information' si les données nécessaires sont présentes dans le snapshot — raisonne à partir de ce que tu vois.


[PRODUITS & QUANTITÉS]
- Il n'existe PAS de module stock dédié. La quantité disponible d'un article se lit directement sur sa FICHE PRODUIT (champ QuantiteDisponible).
- Chaque produit a un TYPE TECHNIQUE qui détermine son rôle dans la chaîne de production :
  * Matière première : achetée à l'extérieur, utilisée comme composant dans les nomenclatures (BOM)
  * Semi-fini : fabriqué en interne, peut servir de composant à un autre produit
  * Produit fini : produit final destiné à la vente
  * Semi-fini+Fini : remplit les deux rôles
- Chaque produit a aussi un TYPE COMMERCIAL : 'Bien' (article physique) ou 'Service'.
- Pour identifier les matières premières, filtrer les produits avec TypeTechnique = Matière première.
- Le coût d'un produit se décompose en : CoutAchat (pour les MP), CoutBom (somme des composants), CoutAutresCharges, et CoutTotal.

[NOMENCLATURE (BOM)]
- Une BOM liste les composants (et leurs quantités) nécessaires pour fabriquer un produit.
- Accessible via Fabrication > Nomenclatures (/BOM).
- La création/édition se fait via /BOMCreate.

[MRP (Planification des besoins matières)]
- Le MRP calcule automatiquement les besoins en composants selon les ordres planifiés.
- Un plan MRP a un STATUT : Brouillon (édition), Sauvegardée (validé), Annulée.
- Le MRP génère des Ordres de Fabrication (OF) et des Ordres d'Achat (OA).
- Détail d'un plan : /MRPDetail. Configuration : /MRPConfig.

[LOGISTIQUE & VÉHICULES]
- Chaque véhicule a : un TYPE DE TRANSPORT (Camion, Fourgonnette...), un TYPE DE CARBURANT (Essence, Diesel, Hybride, Électrique, GNV), et un STATUT (Disponible, En Trajet, Maintenance).
- Suivi GPS temps réel : /Logistique/Tracking.

[CRM & CONTACTS]
- Un contact peut avoir PLUSIEURS RÔLES simultanément : Client, Fournisseur, Employé, Partenaire, Investisseur.
- Pour trouver les fournisseurs : contacts avec le rôle Fournisseur.
- Les Contacts CRM sont distincts des ErpUsers (comptes de connexion à l'ERP).

[UTILISATEURS / EMPLOYÉS]
- Les ErpUsers sont les comptes de connexion à l'ERP (profils employés).
- Pas de module RH dédié. Pour créer un employé : /CreateProfile.

[MODULES ACTIFS]
- Accueil (/Home) · Messagerie (/Messagerie) · Fabrication (/Fabrication) · Logistique (/Logistique/Index) · CRM (/AnnuaireList)

[MODULES NON DISPONIBLES (en développement)]
- Ventes, Achats, Stock dédié, RH, Comptabilité
";

            // --- SNAPSHOT DYNAMIQUE (données réelles au moment de la question) ---
            string snapshotDonnees = $@"
=== DONNÉES ACTUELLES DE L'ERP (snapshot en temps réel) ===

RÉSUMÉ : {countUsers} employé(s), {countProduits} produit(s), {countVehicules} véhicule(s), {countContacts} contact(s) CRM, {countMRP} plan(s) MRP.

LISTE DES PRODUITS ({countProduits}) :
{(lignesProduits.Length > 0 ? lignesProduits.ToString().TrimEnd() : "  (aucun produit enregistré)")}

LISTE DES VÉHICULES ({countVehicules}) :
{(lignesVehicules.Length > 0 ? lignesVehicules.ToString().TrimEnd() : "  (aucun véhicule enregistré)")}

CONTACTS CRM ({countContacts}) :
{(lignesContacts.Length > 0 ? lignesContacts.ToString().TrimEnd() : "  (aucun contact enregistré)")}

PLANS MRP RÉCENTS ({countMRP} au total, 5 derniers affichés) :
{(lignesMRP.Length > 0 ? lignesMRP.ToString().TrimEnd() : "  (aucun plan MRP)")}
";

            string contextualInstruction = systemPromptText + "\n\n" + guideMetier + "\n\n" + snapshotDonnees;

            var history = await _dbContext.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.Timestamp)
                .Take(10)
                .ToListAsync();
            history.Reverse();

            // Trouver l'ID de l'utilisateur IA (GEMINI) pour distinguer ses messages dans l'historique
            int iaUserId = -1;
            try {
                var iaUser = await _dbContext.ErpUsers.FirstOrDefaultAsync(u => u.Login == "GEMINI" || u.Login == "skyra-ia");
                if (iaUser != null) iaUserId = iaUser.Id;
            } catch (Exception exIaUser) {
                Console.WriteLine($"[IA STREAM] Erreur chargement utilisateur IA: {exIaUser.Message}");
            }

            var contentsList = new List<object>();
            string lastRoleAdded = "";

            foreach (var msg in history)
            {
                var txt = msg.Content?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(txt) || txt == userMessage || txt == "\u200B") continue;
                string role = (iaUserId != -1 && msg.SenderId == iaUserId) ? "model" : "user";
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
                    string updatedInstruction = contextualInstruction + "\n\nIMPORTANT : Tu as l'autorisation d'utiliser ton outil de recherche Google Search pour répondre à toute question de l'utilisateur qui concerne internet, l'actualité, ou des sujets hors de l'ERP. N'indique pas que tu es limité ou que tu ne peux pas le faire.";
                    finalPayload = new { 
                        system_instruction = new { parts = new[] { new { text = updatedInstruction } } }, 
                        contents = contentsList,
                        tools = new[] { new { googleSearch = new object() } }
                    };
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
