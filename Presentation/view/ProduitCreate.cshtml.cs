using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            await ChargerProduitsAsync();
        }

        private async Task ChargerProduitsAsync()
        {
            try
            {
                Produits = await _context.Produits
                    .OrderByDescending(p => p.DateCreation)
                    .Take(50)
                    .ToListAsync();

                Console.WriteLine($"DEBUG Produits.OnGetAsync - {Produits.Count} produits chargés");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERREUR Produits.OnGetAsync - {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            Console.WriteLine($"DEBUG Produits.OnPostDeleteAsync - demande suppression id={id}");

            var produit = await _context.Produits.FirstOrDefaultAsync(p => p.Id == id);
            if (produit == null)
            {
                Console.WriteLine($"WARN Produits.OnPostDeleteAsync - produit id={id} introuvable");
                return RedirectToPage(); // recharge la liste
            }

            _context.Produits.Remove(produit);
            await _context.SaveChangesAsync();

            Console.WriteLine($"DEBUG Produits.OnPostDeleteAsync - produit id={id} supprimé");

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            Console.WriteLine("DEBUG Produits.OnPostLogoutAsync - Déconnexion");
            return RedirectToPage("/Index");
        }
    }
}
