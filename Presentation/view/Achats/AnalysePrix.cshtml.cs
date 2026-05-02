// Fichier : Presentation/view/Achats/AnalysePrix.cshtml.cs
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Donnees;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages.Achats
{
    /// <summary>
    /// Page d'analyse avancée des prix.
    /// Sans produitId → sélecteur de produits.
    /// Avec produitId → graphique + simulateur pour ce produit.
    /// Innovation : absent des ERPs concurrents (Odoo, SAP, Dynamics).
    /// </summary>
    public class AnalysePrixModel : PageModel
    {
        private readonly AchatsPrixService _prixService;
        private readonly AchatsService     _achatsService;
        private readonly ErpDbContext      _db;

        public AnalysePrixModel(AchatsPrixService prixService, AchatsService achatsService, ErpDbContext db)
        {
            _prixService   = prixService;
            _achatsService = achatsService;
            _db            = db;
        }

        // ─── Mode sélecteur (produitId == 0) ─────────────────────────────────
        public bool ModeSelecteur { get; set; }
        public List<ProduitAvecHistorique> ProduitsAvecHistorique { get; set; } = new();

        // ─── Mode analyse (produitId > 0) ────────────────────────────────────
        public Produit?             Produit     { get; set; }
        public HistoriquePrixGraphe Graphe      { get; set; } = new();
        public string GrapheJson  { get; set; } = "{}";
        public string SimBaseJson { get; set; } = "[]";

        public class ProduitAvecHistorique
        {
            public int     Id             { get; set; }
            public string  Nom            { get; set; } = string.Empty;
            public string  Reference      { get; set; } = string.Empty;
            public decimal DernierPrix    { get; set; }
            public int     NbAchats       { get; set; }
            public string? DernierFourn   { get; set; }
        }

        // ─── GET ──────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(int produitId = 0)
        {
            var config = await _achatsService.GetConfigAsync();
            if (config?.EstConfigure != true)
                return RedirectToPage("/Achats/Config");

            // ── Mode sélecteur : aucun produit précisé ────────────────────────
            if (produitId == 0)
            {
                ModeSelecteur = true;

                var historiques = await _db.AchatHistoriquesPrix
                    .Include(h => h.Fournisseur)
                    .GroupBy(h => h.ProduitId)
                    .Select(g => new
                    {
                        ProduitId    = g.Key,
                        NbAchats     = g.Count(),
                        DernierPrix  = g.OrderByDescending(h => h.DateAchat).First().PrixUnitaireHT,
                        DernierFourn = g.OrderByDescending(h => h.DateAchat).First().Fournisseur!.FullName
                    })
                    .ToListAsync();

                var produitIds = historiques.Select(h => h.ProduitId).ToList();
                var produits   = await _db.Produits
                    .Where(p => produitIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                ProduitsAvecHistorique = historiques
                    .Where(h => produits.ContainsKey(h.ProduitId))
                    .Select(h => new ProduitAvecHistorique
                    {
                        Id           = h.ProduitId,
                        Nom          = produits[h.ProduitId].Nom,
                        Reference    = produits[h.ProduitId].Reference,
                        DernierPrix  = h.DernierPrix,
                        NbAchats     = h.NbAchats,
                        DernierFourn = h.DernierFourn
                    })
                    .OrderBy(p => p.Nom)
                    .ToList();

                return Page();
            }

            // ── Mode analyse : produit précisé ────────────────────────────────
            Produit = await _db.Produits.FindAsync(produitId);
            if (Produit == null) return NotFound();

            Graphe = await _prixService.GetHistoriqueGrapheAsync(produitId);
            var simBase = await _prixService.SimulerHaussePrixAsync(produitId, 10m);

            var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            GrapheJson  = JsonSerializer.Serialize(Graphe,   opts);
            SimBaseJson = JsonSerializer.Serialize(simBase,  opts);

            return Page();
        }

        // ─── Handler AJAX : simulation à la volée ─────────────────────────────
        public async Task<IActionResult> OnGetSimulerAsync(int produitId, decimal hausse)
        {
            var impacts = await _prixService.SimulerHaussePrixAsync(produitId, hausse);
            return new JsonResult(impacts, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
    }
}
