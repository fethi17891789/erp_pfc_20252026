using Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erppfc20252026.Pages
{
    public class ProduitsModel : PageModel
    {
        private readonly ErpDbContext _context;

        public ProduitsModel(ErpDbContext context)
        {
            _context = context;
        }

        public List<Produit> Produits { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                // Récupérer tous les produits avec les infos essentielles
                Produits = await _context.Produits
                    .OrderByDescending(p => p.DateCreation)
                    .Take(50) // Limite pour la performance
                    .ToListAsync();

                Console.WriteLine($"DEBUG Produits.OnGetAsync - {Produits.Count} produits chargés");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERREUR Produits.OnGetAsync - {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            // Logique de déconnexion (à implémenter selon ton système d'auth)
            Console.WriteLine("DEBUG Produits.OnPostLogoutAsync - Déconnexion");
            return RedirectToPage("/Index"); // ou ta page de login
        }
    }
}
