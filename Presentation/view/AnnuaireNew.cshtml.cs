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
                // ETAPE 1 : Localisation du Site Officiel (Grounding Intelligence)
                Console.WriteLine($"[Magic CRM] ETAPE 1 : Identification du site officiel pour \"{description}\"...");
                string searchPrompt = $@"Quelle est l'URL du site web officiel pour l'entité décrite ici : ""{description}"" ? 
Réponds UNIQUEMENT en JSON : {{ ""Website"": ""https://..."" }}";
                var (searchJson, _) = await _iaService.AskAiJsonAsync("Tu es un expert en recherche web.", searchPrompt);
                
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
                Console.WriteLine($"[Magic CRM] ETAPE 3 : Arbitrage intelligent sur {(scrapedData?.Phone?.Split('|').Length ?? 0)} numéros détectés...");
                string synthesisPrompt = $@"Génère la fiche contact finale la plus fiable en arbitrant entre ces sources.
DESCRIPTION UTILISATEUR : {description}
DONNÉES TERRAIN (Site {discoveredUrl}) :
- Nom trouvé : {scrapedData?.FullName}
- Email trouvé : {scrapedData?.Email}
- Téléphones candidats trouvés : {scrapedData?.Phone}

CONSIGNES :
1. Choisis le numéro qui semble être le standard professionnel réel (fréquence et format).
2. Valide l'email.
3. Rédige un résumé pro basé sur ces faits réels.

RÉPONDS UNIQUEMENT EN JSON VALIDE :
{{
  ""FullName"": ""Nom Légal Précis"",
  ""Email"": ""email@contact.com"",
  ""Phone"": ""+33..."",
  ""Website"": ""{discoveredUrl}"",
  ""Comment"": ""Résumé professionnel basé sur l'arbitrage des sources.""
}}";

                var (finalJson, modelName) = await _iaService.AskAiJsonAsync("Tu es un expert en intelligence économique. Ta mission est de fournir la source de vérité finale.", synthesisPrompt);
                
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
}
