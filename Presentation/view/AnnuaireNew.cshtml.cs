using Donnees;
using Metier.CRM;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Linq;
using System.Threading.Tasks;

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

        public IActionResult OnGetValidatePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return new JsonResult(new { IsValid = false });

            bool isValid = _validationService.ValidatePhone(phone, "FR");
            return new JsonResult(new { IsValid = isValid });
        }

        public async Task<IActionResult> OnGetValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return new JsonResult(new { IsValid = false });

            bool isValid = await _validationService.ValidateEmailAsync(email);
            return new JsonResult(new { IsValid = isValid });
        }

        public async Task<IActionResult> OnGetEnrichFromWebsite(string website)
        {
            if (string.IsNullOrWhiteSpace(website))
                return new JsonResult(new { FullName = "", Comment = "" });

            var info = await _validationService.ExtractCompanyInfoAsync(website);
            return new JsonResult(new { FullName = info.FullName, Comment = info.Comment });
        }

        public async Task<IActionResult> OnPostAiMagicEnrichAsync([FromBody] string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return new JsonResult(new { error = "Description vide" });

            string systemPrompt = @"Tu es un expert en intelligence économique et gestion de données B2B. 
Ta mission est d'enrichir une fiche CRM SKYRA avec une précision IRREPROCHABLE.

ETAPES DE TON TRAVAIL :
1. ANALYSE : Identifie l'entreprise mentionnée et sa localisation.
2. RECHERCHE : Utilise Google Search pour trouver les mentions légales, le site officiel et les annuaires professionnels.
3. EXTRACTION :
   - FullName : Trouve le NOM LEGAL COMPLET (ex: L'OREAL SA, MICROSOFT FRANCE SAS). Ne donne pas que le nom commercial.
   - Website : Trouve l'URL exacte du site institutionnel.
   - Email : Trouve l'adresse de contact principale (info@, contact@, sales@). Si tu connais le format des emails de la boîte (ex: p.nom@entreprise.com) et le nom de la personne, déduis-le.
   - Phone : Extrais le numéro du siège social ou du standard.
   - Comment : Rédige une description succincte mais riche (secteur, effectif estimé, spécialité).

REGLES D'OR :
- NE JAMAIS INVENTER. Si un champ est vide, c'est que l'info n'existe pas publiquement.
- FORMATAGE : Retourne UNIQUEMENT un objet JSON valide.

{
  ""FullName"": ""Nom Légal Complet"",
  ""Email"": ""contact@domaine.com"",
  ""Phone"": ""+33 1 ..."",
  ""Website"": ""https://www.site.com"",
  ""Comment"": ""Description experte...""
}";

            string result = await _iaService.AskAiJsonAsync(systemPrompt, description);
            return Content(result, "application/json");
        }
    }
}
