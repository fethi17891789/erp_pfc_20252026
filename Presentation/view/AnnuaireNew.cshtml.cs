using Donnees;
using Metier.CRM;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Nodes;

namespace erp_pfc_20252026.Pages
{
    public class AnnuaireNewModel : PageModel
    {
        private readonly AnnuaireService _annuaireService;
        private readonly ValidationService _validationService;
        private readonly Metier.IAService _iaService;

        public AnnuaireNewModel(AnnuaireService annuaireService, ValidationService validationService, Metier.IAService iaService)
        {
            _annuaireService = annuaireService;
            _validationService = validationService;
            _iaService = iaService;
        }

        [BindProperty]
        public Contact CurrentContact { get; set; } = new Contact();

        [BindProperty]
        public string SelectedRoles { get; set; } = string.Empty;

        public string SuccessMessage { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id.HasValue)
            {
                var contact = await _annuaireService.GetContactByIdAsync(id.Value);
                if (contact == null)
                    return RedirectToPage("/AnnuaireList");

                CurrentContact = contact;

                // Extraction des roles pour peupler le JS côté Front
                var rolesList = Enum.GetValues<ContactRole>()
                                    .Where(r => r != ContactRole.None && contact.Roles.HasFlag(r))
                                    .Select(r => r.ToString())
                                    .ToList();
                SelectedRoles = string.Join(",", rolesList);
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Veuillez vérifier les champs obligatoires.";
                return Page();
            }

            // --- Validation stricte (Bloque la sauvegarde si infos erronées) ---
            if (!string.IsNullOrWhiteSpace(CurrentContact.Email))
            {
                bool isEmailValid = await _validationService.ValidateEmailAsync(CurrentContact.Email);
                if (!isEmailValid)
                {
                    ModelState.AddModelError("CurrentContact.Email", "L'adresse email fournie est invalide ou ne la reçoit pas.");
                }
            }

            if (!string.IsNullOrWhiteSpace(CurrentContact.Phone))
            {
                bool isPhoneValid = _validationService.ValidatePhone(CurrentContact.Phone, "FR");
                if (!isPhoneValid)
                {
                    ModelState.AddModelError("CurrentContact.Phone", "Le numéro de téléphone est invalide.");
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                ErrorMessage = string.Join(" ", errors);
                return Page();
            }
            // -------------------------------------------------------------------

            // Parsing des rôles (ex: "Client,Employe")
            ContactRole finalRole = ContactRole.None;
            if (!string.IsNullOrWhiteSpace(SelectedRoles))
            {
                var parts = SelectedRoles.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (Enum.TryParse<ContactRole>(part.Trim(), true, out var r))
                    {
                        finalRole |= r;
                    }
                }
            }
            // Si aucun n'est sélectionné, on peut forcer un rôle par défaut ou laisser "None"
            CurrentContact.Roles = finalRole;

            try
            {
                // Note : Pas d'upload d'image pour l'instant (Phase 1)
                await _annuaireService.SaveContactAsync(CurrentContact);
                SuccessMessage = "Le contact a été enregistré avec succès.";
                
                // Redirection ou rafraîchissement
                return RedirectToPage("/AnnuaireList");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erreur lors de l'enregistrement : {ex.Message}";
                return Page();
            }
        }

        // ======================= ENDPOINTS RELATIONS =======================

        public async Task<IActionResult> OnGetRelationsAsync(int contactId)
        {
            var relations = await _annuaireService.GetRelationsForContactAsync(contactId);
            var result = relations.Select(r => new
            {
                id = r.Id,
                sourceId = r.SourceContactId,
                sourceName = r.SourceContact?.FullName ?? "?",
                targetId = r.TargetContactId,
                targetName = r.TargetContact?.FullName ?? "?",
                relationType = r.RelationType
            });
            return new JsonResult(result);
        }

        public async Task<IActionResult> OnGetAllContactsForRelationAsync(int excludeId)
        {
            var contacts = await _annuaireService.GetAllContactsAsync();
            var result = contacts
                .Where(c => c.Id != excludeId)
                .Select(c => new { id = c.Id, name = c.FullName });
            return new JsonResult(result);
        }

        public async Task<IActionResult> OnPostAddRelationAsync([FromBody] AddRelationDto dto)
        {
            if (dto == null || dto.SourceId <= 0 || dto.TargetId <= 0 || string.IsNullOrWhiteSpace(dto.RelationType))
                return new JsonResult(new { ok = false, message = "Données incomplètes." });

            var relation = await _annuaireService.AddRelationAsync(dto.SourceId, dto.TargetId, dto.RelationType.Trim());
            var source = await _annuaireService.GetContactByIdAsync(dto.SourceId);
            var target = await _annuaireService.GetContactByIdAsync(dto.TargetId);

            return new JsonResult(new
            {
                ok = true,
                id = relation.Id,
                sourceId = dto.SourceId,
                sourceName = source?.FullName ?? "?",
                targetId = dto.TargetId,
                targetName = target?.FullName ?? "?",
                relationType = dto.RelationType.Trim()
            });
        }

        public async Task<IActionResult> OnPostDeleteRelationAsync([FromBody] int id)
        {
            await _annuaireService.DeleteRelationAsync(id);
            return new JsonResult(new { ok = true });
        }

        // ======================= ENDPOINTS API AJAX =======================

        public async Task<IActionResult> OnGetEnrichFromWebsite(string website)
        {
            if (string.IsNullOrWhiteSpace(website))
                return new JsonResult(new { FullName = "", Comment = "" });

            var info = await _validationService.ExtractCompanyInfoAsync(website);
            return new JsonResult(new { FullName = info.FullName, Comment = info.Comment });
        }

        public async Task<JsonResult> OnGetValidateEmail(string email)
        {
            var isValid = await _validationService.ValidateEmailAsync(email);
            return new JsonResult(new { isValid });
        }

        public JsonResult OnGetValidatePhone(string phone)
        {
            var isValid = _validationService.ValidatePhone(phone);
            return new JsonResult(new { isValid });
        }

        public async Task<IActionResult> OnPostAiMagicEnrichAsync([FromBody] string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return new JsonResult(new { error = "Description vide" });

            try
            {
                string searchPrompt = $@"Tâche : Trouver l'URL du site officiel pour ""{description}"".
INSTRUCTION STRICTE : Utilise Google Search. Si tu n'es pas ABSOLUMENT SÛR du domaine, ou si l'entreprise n'a pas de site web propre, renvoie une chaîne vide pour 'Website'. N'invente JAMAIS d'URL.
Réponds UNIQUEMENT en JSON : {{ ""Website"": ""https://..."" }}";
                var (searchJson, _) = await _iaService.AskAiJsonAsync("Tu es un expert en recherche web, extrêmement strict. Tu ne devines jamais, tu vérifies la réalité de tes sources.", searchPrompt, enableSearch: true);
                
                string? discoveredUrl = null;
                try {
                    using var doc = System.Text.Json.JsonDocument.Parse(searchJson);
                    if (doc.RootElement.TryGetProperty("Website", out var urlProp)) discoveredUrl = urlProp.GetString();
                } catch { }

                // ETAPE 2 : Scan Radar Profond (The Spider)
                CompanyInfo? scrapedData = null;
                if (!string.IsNullOrWhiteSpace(discoveredUrl) && discoveredUrl.ToLower() != "null" && discoveredUrl.StartsWith("http"))
                {
                    Console.WriteLine($"[Magic CRM] ETAPE 2 : Scraping radar approfondi sur {discoveredUrl}...");
                    scrapedData = await _validationService.ExtractCompanyInfoAsync(discoveredUrl);
                }

                // ETAPE 3 : Arbitrage et Synthèse Finale (Le Cerveau)
                bool hasTerrainData = scrapedData != null && (!string.IsNullOrEmpty(scrapedData.Email) || !string.IsNullOrEmpty(scrapedData.Phone) || !string.IsNullOrEmpty(scrapedData.FullName));
                Console.WriteLine($"[Magic CRM] ETAPE 3 : Arbitrage intelligent. Données terrain valides : {hasTerrainData}");
                
                string synthesisPrompt = $@"Description : {description}
URL examinée : {discoveredUrl}
{(hasTerrainData ? 
  $@"DONNÉES TERRAIN EXTRAITES :
- Nom trouvé : {scrapedData?.FullName}
- Email trouvé : {scrapedData?.Email}
- Tel candidats : {scrapedData?.Phone}" 
  : "AUCUNE DONNÉE TERRAIN EXTRAITE. LE SITE EST INACCESSIBLE OU VIDE.")}

CONSIGNES ANTI-HALLUCINATION :
1. Ne JAMAIS inventer de faux numéros génériques (ex: 01 23 45 67 89) ou de faux emails.
2. Si tu ne trouves rien de fiable à 100%, tu DOIS renvoyer des champs vides ("""").
{(hasTerrainData ? "3. Arbitre les données terrain extraites pour trouver le numéro standard." : "3. Puisque le terrain est vide, tente une de trouver l'information publique via Google Search. Si introuvable, laisse vide.")}

RÉPONDS UNIQUEMENT EN JSON VALIDE :
{{
  ""FullName"": ""Nom réel"",
  ""Email"": """",
  ""Phone"": """",
  ""Website"": ""{discoveredUrl ?? ""}"",
  ""Comment"": ""Résumé de ce qui a été réellement trouvé...""
}}";

                var (finalJson, modelName) = await _iaService.AskAiJsonAsync("Tu es un expert en OSINT très strict. Règle d'or : refuser l'invention. Une donnée manquante vaut mieux qu'une donnée fausse.", synthesisPrompt, enableSearch: true);
                
                var finalObj = System.Text.Json.Nodes.JsonNode.Parse(finalJson)?.AsObject();
                if (finalObj != null) {
                    finalObj["ModelUsed"] = modelName;
                    Console.WriteLine($"[Magic CRM] Synthèse Spider terminée via {modelName}");
                    return Content(finalObj.ToJsonString(), "application/json");
                }

                return Content(finalJson, "application/json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Magic CRM] ERREUR CRITIQUE Orchestration : {ex.Message}");
                return new JsonResult(new { error = $"Erreur interne : {ex.Message}" });
            }
        }
    }

    public class AddRelationDto
    {
        public int SourceId { get; set; }
        public int TargetId { get; set; }
        public string RelationType { get; set; } = string.Empty;
    }
}
