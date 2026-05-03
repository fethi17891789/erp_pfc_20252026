// Fichier : Presentation/view/Settings/EmailConfig.cshtml.cs
using System;
using System.Threading.Tasks;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages.Settings
{
    public class EmailConfigModel : PageModel
    {
        private readonly AchatsGmailService _gmailService;

        public EmailConfigModel(AchatsGmailService gmailService)
        {
            _gmailService = gmailService;
        }

        public bool   EstConnecte   { get; set; }
        public string? EmailConnecte { get; set; }
        public string? MessageSucces  { get; set; }
        public string? MessageErreur  { get; set; }

        public async Task OnGetAsync()
        {
            EstConnecte   = await _gmailService.EstConfigureAsync();
            EmailConnecte = await _gmailService.GetEmailConfigureAsync();

            if (TempData["Succes"]  is string s) MessageSucces = s;
            if (TempData["Erreur"]  is string e) MessageErreur = e;
        }

        // ── Lancer le flow OAuth2 ──────────────────────────────────────────────
        public IActionResult OnPostConnecterGmail()
        {
            string redirectUri = $"{Request.Scheme}://{Request.Host}/Settings/GmailCallback";
            string state       = Guid.NewGuid().ToString("N");

            // Stocker le state en session pour vérification au retour
            HttpContext.Session.SetString("OAuthState", state);

            string url = _gmailService.GenererUrlAutorisation(redirectUri, state);
            return Redirect(url);
        }

        // ── Déconnecter Gmail ─────────────────────────────────────────────────
        public async Task<IActionResult> OnPostDeconnecterAsync()
        {
            await _gmailService.DeconnecterAsync();
            TempData["Succes"] = "Compte Gmail déconnecté.";
            return RedirectToPage();
        }
    }
}
