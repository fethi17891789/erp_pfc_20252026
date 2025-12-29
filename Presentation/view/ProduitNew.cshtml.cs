using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages
{
    public class ProduitNewModel : PageModel
    {
        public void OnGet()
        {
            // Préparation éventuelle des listes déroulantes, etc.
        }

        public IActionResult OnPost()
        {
            // Plus tard : récupérer les données du formulaire, sauvegarder, rediriger
            return RedirectToPage("/Produit"); // ou autre page de ton choix
        }
    }
}
