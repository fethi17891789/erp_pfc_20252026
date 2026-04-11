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

        public AnnuaireNewModel(AnnuaireService annuaireService, ValidationService validationService)
        {
            _annuaireService = annuaireService;
            _validationService = validationService;
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
    }
}
