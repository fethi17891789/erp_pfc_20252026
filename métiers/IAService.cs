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

        public async Task<string> CallGeminiAsync(string userMessage)
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
                
                // Forcer le nom correct si l'ancien 'gemini-1.5-flash' classique bloque
                if (modelName == "gemini-1.5-flash")
                {
                    modelName = "gemini-1.5-flash-latest";
                }
                
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = userMessage }
                            }
                        }
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Appel HTTP à Google
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    // Si le modèle est introuvable (erreur 404 de l'API Gemini)
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        var listUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                        var listResponse = await _httpClient.GetAsync(listUrl);
                        if (listResponse.IsSuccessStatusCode)
                        {
                            var listBody = await listResponse.Content.ReadAsStringAsync();
                            using var listDoc = JsonDocument.Parse(listBody);
                            var sbLog = new StringBuilder();
                            sbLog.AppendLine("Erreur : Le nom de modèle paramétré n'existe pas ou n'est pas accessible avec cette clé.\n");
                            sbLog.AppendLine("Voici la liste exacte des modèles que ta clé API supporte en ce moment : \n");
                            
                            foreach (var m in listDoc.RootElement.GetProperty("models").EnumerateArray())
                            {
                                var n = m.GetProperty("name").GetString();
                                var supp = false;
                                if (m.TryGetProperty("supportedGenerationMethods", out var methods))
                                {
                                    foreach (var method in methods.EnumerateArray())
                                    {
                                        if (method.GetString() == "generateContent") supp = true;
                                    }
                                }
                                if (supp) sbLog.AppendLine($"- {n} (génère du texte)");
                            }
                            
                            sbLog.AppendLine("\n**Copiez l'un des noms ci-dessus sans 'models/' (par exemple 'gemini-pro') et mettez-le dans le champ ModelName de pgAdmin.**");
                            return sbLog.ToString();
                        }
                    }

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
