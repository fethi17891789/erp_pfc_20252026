// Fichier : Presentation/view/Settings/GmailCallback.cshtml.cs
using System;
using System.Threading.Tasks;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages.Settings
{
    /// <summary>
    /// Page de retour OAuth2 Google.
    /// Google redirige ici après que l'utilisateur a autorisé l'accès.
    /// </summary>
    public class GmailCallbackModel : PageModel
    {
        private readonly AchatsGmailService _gmailService;

        public GmailCallbackModel(AchatsGmailService gmailService)
        {
            _gmailService = gmailService;
        }

        public async Task<IActionResult> OnGetAsync(string? code, string? state, string? error)
        {
            // Accès refusé par l'utilisateur
            if (!string.IsNullOrEmpty(error))
            {
                TempData["Erreur"] = "Connexion Gmail annulée.";
                return RedirectToPage("/Settings/EmailConfig");
            }

            // Vérification du state anti-CSRF
            var stateAttendu = HttpContext.Session.GetString("OAuthState");
            if (string.IsNullOrEmpty(code) || state != stateAttendu)
            {
                TempData["Erreur"] = "Erreur de sécurité OAuth. Veuillez réessayer.";
                return RedirectToPage("/Settings/EmailConfig");
            }

            HttpContext.Session.Remove("OAuthState");

            try
            {
                string redirectUri = $"{Request.Scheme}://{Request.Host}/Settings/GmailCallback";
                await _gmailService.EchangerCodeEtSauvegarderAsync(code, redirectUri);
                TempData["Succes"] = "Compte Gmail connecté avec succès ! SKYRA peut maintenant envoyer des emails.";
            }
            catch (Exception ex)
            {
                TempData["Erreur"] = $"Échec de la connexion Gmail : {ex.Message}";
            }

            return RedirectToPage("/Settings/EmailConfig");
        }
    }
}
