using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages
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

                Console.WriteLine($"DEBUG Produits.OnGetAsync - {Produits.Count} produits charg�s");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERREUR Produits.OnGetAsync - {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            Console.WriteLine($"DEBUG Produits.OnPostDeleteAsync - demande suppression id={id}");

            var produit = await _context.Produits
                .FirstOrDefaultAsync(p => p.Id == id);

            if (produit == null)
            {
                Console.WriteLine($"WARN Produits.OnPostDeleteAsync - produit id={id} introuvable");
                return RedirectToPage();
            }

            // 1) MRP : existe-t-il au moins une ligne MRP pour ce produit
            // dont le plan N'EST PAS en statut "Annulee" ?
            var mrpLinesQuery = _context.MRPPlanLignes
                .Include(l => l.MRPPlan)
                .Where(l => l.ProduitId == id);

            bool hasActiveMrp = await mrpLinesQuery
                .AnyAsync(l => l.MRPPlan != null && l.MRPPlan.Statut != "Annulee");

            if (hasActiveMrp)
            {
                Console.WriteLine($"WARN Produits.OnPostDeleteAsync - produit id={id} utilis� dans un plan MRP non annul�, suppression refus�e");
                TempData["Error"] = "Impossible de supprimer ce produit, il est utilis� dans au moins une planification MRP non annul�e.";
                return RedirectToPage();
            }

            // Ici : soit aucune ligne MRP, soit toutes les planifs sont en statut Annulee.
            // On peut donc supprimer les lignes MRP associ�es (plans annul�s) pour ce produit.
            var mrpLinesForProduct = await mrpLinesQuery.ToListAsync();
            if (mrpLinesForProduct.Any())
            {
                Console.WriteLine($"DEBUG Produits.OnPostDeleteAsync - suppression de {mrpLinesForProduct.Count} ligne(s) MRP (plans annul�s) pour produit id={id}");
                _context.MRPPlanLignes.RemoveRange(mrpLinesForProduct);
            }

            // 2) BOM : R�cup�rer toutes les BOM o� ce produit est le produit fini
            var bomsForProduct = await _context.Boms
                .Include(b => b.Lignes)
                .Where(b => b.ProduitId == id)
                .ToListAsync();

            // Vraie nomenclature = au moins une ligne
            bool hasRealBom = bomsForProduct
                .Any(b => b.Lignes != null && b.Lignes.Count > 0);

            if (hasRealBom)
            {
                Console.WriteLine($"WARN Produits.OnPostDeleteAsync - produit id={id} poss�de une nomenclature, suppression refus�e");
                TempData["Error"] = "Impossible de supprimer ce produit, il poss�de d�j� une nomenclature.";
                return RedirectToPage();
            }

            // BOMs pr�sentes mais sans lignes : on les supprime puis le produit
            if (bomsForProduct.Any())
            {
                Console.WriteLine($"DEBUG Produits.OnPostDeleteAsync - suppression de {bomsForProduct.Count} BOM(s) vides pour produit id={id}");
                _context.Boms.RemoveRange(bomsForProduct);
            }

            _context.Produits.Remove(produit);
            await _context.SaveChangesAsync();

            Console.WriteLine($"DEBUG Produits.OnPostDeleteAsync - produit id={id} supprim�");

            TempData["Success"] = "Produit supprim� avec succ�s.";
            return RedirectToPage();
        }
    }
}
