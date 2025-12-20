// Fichier : Presentation/view/Home.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages
{
    public class HomeModel : PageModel
    {
        public void OnGet()
        {
            // Pour l’instant aucune logique : simple écran d’accueil factice.
        }

        // Appelé par le formulaire du popup (asp-page-handler="Logout")
        public IActionResult OnPostLogout()
        {
            // Ici plus tard : suppression session / cookies si besoin

            // Redirection vers la page de choix profil / connexion
            return RedirectToPage("/ChooseProfile"); // adapte le chemin si ta page a un autre nom
        }
    }
}
