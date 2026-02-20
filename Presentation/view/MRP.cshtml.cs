// Fichier : Pages/MRP.cshtml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Donnees;
using Metier.MRP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages
{
    public class MRPModel : PageModel
    {
        private readonly MRPConfigService _configService;
        private readonly ErpDbContext _db;

        public MRPModel(MRPConfigService configService, ErpDbContext db)
        {
            _configService = configService;
            _db = db;
        }

        // Produits pour le popup
        public string? ProductsJson { get; set; }

        // Liste des plans a afficher en cartes
        public List<MRPPlan> Plans { get; set; } = new List<MRPPlan>();

        public async Task<IActionResult> OnGetAsync()
        {
            var cfg = await _configService.GetConfigAsync();
            if (cfg == null)
            {
                return RedirectToPage("/MRPConfig");
            }

            var produits = await _db.Produits
                .OrderBy(p => p.Nom)
                .Select(p => new
                {
                    id = p.Id,
                    nom = p.Nom,
                    type = p.TypeTechnique,
                    hasBom = _db.Boms.Any(b => b.ProduitId == p.Id)
                })
                .ToListAsync();

            ProductsJson = JsonSerializer.Serialize(produits);

            // Charger les plans MRP depuis la BDD, les plus recents d'abord
            Plans = await _db.MRPPlans
                .OrderByDescending(p => p.DateCreation)
                .AsNoTracking()
                .ToListAsync();

            return Page();
        }

        // Endpoint AJAX pour recharger les produits
        public async Task<IActionResult> OnGetRefreshProductsAsync()
        {
            var produits = await _db.Produits
                .OrderBy(p => p.Nom)
                .Select(p => new
                {
                    id = p.Id,
                    nom = p.Nom,
                    type = p.TypeTechnique,
                    hasBom = _db.Boms.Any(b => b.ProduitId == p.Id)
                })
                .ToListAsync();

            return new JsonResult(produits);
        }

        public IActionResult OnPostLogout()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/BDDView");
        }

        // Helpers simples pour la vue

        public string FormatDateRange(DateTime debut, DateTime fin)
        {
            return string.Format("{0:dd/MM/yyyy} -> {1:dd/MM/yyyy}", debut, fin);
        }

        public string GetPlanStatusLabel(string statut)
        {
            if (string.IsNullOrWhiteSpace(statut))
                return "Brouillon";

            // Normaliser les variantes
            if (string.Equals(statut, "Annulee", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(statut, "Annulée", StringComparison.OrdinalIgnoreCase))
                return "Annulee";

            if (string.Equals(statut, "Terminee", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(statut, "Terminée", StringComparison.OrdinalIgnoreCase))
                return "Terminee";

            if (string.Equals(statut, "Sauvegardee", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(statut, "Sauvegardée", StringComparison.OrdinalIgnoreCase))
                return "Sauvegardee";

            return statut;
        }
    }
}
