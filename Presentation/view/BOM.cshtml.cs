using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Donnees;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages.Fabrication
{
    public class BOMModel : PageModel
    {
        private readonly ErpDbContext _context;

        public BOMModel(ErpDbContext context)
        {
            _context = context;
        }

        // ViewModel pour l'affichage des cartes
        public class BomCardItem
        {
            public int ProduitId { get; set; }
            public string Reference { get; set; } = string.Empty;
            public string Nom { get; set; } = string.Empty;
            public decimal Prix { get; set; }
            public string? Image { get; set; }

            // Statut BOM
            public bool HasBom { get; set; }
            public string VersionBom { get; set; } = string.Empty;
            public string CodeBom { get; set; } = string.Empty;

            public int? BomId { get; set; }

            // Pour filtrer/afficher si besoin
            public TypeTechniqueProduit TypeTechnique { get; set; }
        }

        public List<BomCardItem> BomItems { get; set; } = new();

        public async Task OnGetAsync(string? searchTerm)
        {
            // 1) Tous les produits SAUF matières premières
            var productsQuery = _context.Produits
                .AsNoTracking()
                .Where(p => p.TypeTechnique != TypeTechniqueProduit.MatierePremiere);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowered = searchTerm.ToLower();
                productsQuery = productsQuery.Where(p =>
                    p.Nom.ToLower().Contains(lowered) ||
                    (p.Reference != null && p.Reference.ToLower().Contains(lowered)));
            }

            var products = await productsQuery.ToListAsync();

            // 2) Boms par produit (on prend la première pour l’instant)
            var bomsByProduct = await _context.Boms
                .AsNoTracking()
                .GroupBy(b => b.ProduitId)
                .Select(g => new
                {
                    ProduitId = g.Key,
                    BomId = g.Min(b => b.Id)
                })
                .ToListAsync();

            var dictBoms = bomsByProduct.ToDictionary(x => x.ProduitId, x => x.BomId);

            // 3) Mapping produits -> cartes
            BomItems = products
                .Select(p =>
                {
                    var hasBom = dictBoms.ContainsKey(p.Id);
                    int? bomId = hasBom ? dictBoms[p.Id] : (int?)null;

                    return new BomCardItem
                    {
                        ProduitId = p.Id,
                        Reference = p.Reference ?? string.Empty,
                        Nom = p.Nom,
                        Prix = p.PrixVente,
                        Image = p.Image,
                        HasBom = hasBom,
                        VersionBom = hasBom ? "v1" : string.Empty,
                        CodeBom = string.Empty,
                        BomId = bomId,
                        TypeTechnique = p.TypeTechnique
                    };
                })
                .ToList();
        }
    }
}
