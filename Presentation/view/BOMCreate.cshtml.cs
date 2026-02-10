// Fichier : Presentation/view/BOMCreate.cshtml.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Donnees;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages
{
    public class BOMCreateModel : PageModel
    {
        private readonly ErpDbContext _context;

        public BOMCreateModel(ErpDbContext context)
        {
            _context = context;
        }

        public List<ProduitViewModel> Produits { get; set; } = new();

        [BindProperty]
        public int? SelectedProduitId { get; set; }

        [BindProperty]
        [StringLength(200, ErrorMessage = "Le nom ne peut pas dépasser 200 caractères.")]
        public string? ProduitNomSaisi { get; set; }

        [BindProperty]
        public List<BomLigneInput> Lignes { get; set; } = new();

        [BindProperty]
        public int? BomId { get; set; }

        // Valeur de CoutBom pour affichage "gros" dans la vue
        public decimal CoutBomCalcule { get; set; }

        public class ProduitViewModel
        {
            public int Id { get; set; }
            public string Nom { get; set; } = string.Empty;
            public string Reference { get; set; } = string.Empty;
            public decimal PrixVente { get; set; }
            public decimal CoutBom { get; set; }

            // Type technique réel de ton modèle
            public TypeTechniqueProduit TypeTechnique { get; set; }

            // Coût d'achat (MP)
            public decimal CoutAchat { get; set; }

            // Coût unitaire utilisé dans la BOM
            public decimal CoutUnitairePourBom
            {
                get
                {
                    // MP -> CoutAchat
                    // Semi-fini / Fini / SemiFiniEtFini -> CoutBom
                    return TypeTechnique == TypeTechniqueProduit.MatierePremiere
                        ? CoutAchat
                        : CoutBom;
                }
            }
        }

        public class BomLigneInput
        {
            public int Index { get; set; }
            public int? ComposantProduitId { get; set; }
            public string? ComposantNomSaisi { get; set; }

            public decimal? PrixUnitaire { get; set; }
            public decimal? Quantite { get; set; }
            public decimal? AutresCharges { get; set; }
        }

        public class BomTreeNode
        {
            public int ProduitId { get; set; }
            public string NomProduit { get; set; } = string.Empty;
            public string Reference { get; set; } = string.Empty;
            public decimal QuantiteTotale { get; set; }
            public List<BomTreeNode> Enfants { get; set; } = new();
        }

        public BomTreeNode? BomTreeRoot { get; set; }

        public async Task OnGetAsync(int? fromProductId, int? fromComponentId, int? rowIndex, int? bomId)
        {
            await ChargerProduitsAsync();

            BomId = bomId;

            // Produit principal
            if (fromProductId.HasValue)
            {
                var p = Produits.FirstOrDefault(x => x.Id == fromProductId.Value);
                if (p != null)
                {
                    SelectedProduitId = p.Id;
                    ProduitNomSaisi = string.IsNullOrEmpty(p.Reference)
                        ? p.Nom
                        : $"{p.Nom} ({p.Reference})";

                    CoutBomCalcule = p.CoutBom;
                }

                if (!BomId.HasValue)
                {
                    var existingBom = await _context.Boms
                        .Include(b => b.Lignes)
                        .ThenInclude(l => l.ComposantProduit)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(b => b.ProduitId == fromProductId.Value);

                    if (existingBom != null)
                    {
                        BomId = existingBom.Id;
                        Lignes = existingBom.Lignes
                            .Select((l, idx) => new BomLigneInput
                            {
                                Index = idx,
                                ComposantProduitId = l.ComposantProduitId,
                                ComposantNomSaisi = l.ComposantProduit == null
                                    ? string.Empty
                                    : (string.IsNullOrEmpty(l.ComposantProduit.Reference)
                                        ? l.ComposantProduit.Nom
                                        : $"{l.ComposantProduit.Nom} ({l.ComposantProduit.Reference})"),
                                PrixUnitaire = l.PrixUnitaire,
                                Quantite = l.Quantite,
                                AutresCharges = l.AutresCharges
                            })
                            .ToList();
                    }
                }
            }

            Lignes ??= new List<BomLigneInput>();

            // Retour depuis ProduitNew pour un composant
            if (fromComponentId.HasValue && rowIndex.HasValue && rowIndex.Value >= 0)
            {
                while (Lignes.Count <= rowIndex.Value)
                {
                    Lignes.Add(new BomLigneInput
                    {
                        Index = Lignes.Count,
                        Quantite = 1,
                        PrixUnitaire = 0,
                        AutresCharges = 0
                    });
                }

                var ligne = Lignes[rowIndex.Value];
                var produit = Produits.FirstOrDefault(x => x.Id == fromComponentId.Value);
                if (produit != null)
                {
                    ligne.ComposantProduitId = produit.Id;
                    ligne.ComposantNomSaisi = string.IsNullOrEmpty(produit.Reference)
                        ? produit.Nom
                        : $"{produit.Nom} ({produit.Reference})";

                    // Application de la règle côté serveur
                    ligne.PrixUnitaire = produit.CoutUnitairePourBom;
                }
            }

            CoutBomCalcule = CalculerCoutBomLocal(Lignes);

            if (SelectedProduitId.HasValue)
            {
                BomTreeRoot = await ConstruireBomTreeAsync(SelectedProduitId.Value);
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await ChargerProduitsAsync();

            Lignes = (Lignes ?? new List<BomLigneInput>())
                .Where(l => l.ComposantProduitId.HasValue)
                .Select((l, idx) =>
                {
                    l.Index = idx;
                    return l;
                })
                .ToList();

            if (!SelectedProduitId.HasValue)
            {
                ModelState.AddModelError(nameof(SelectedProduitId), "Vous devez sélectionner un produit principal.");
            }

            if (!ModelState.IsValid)
            {
                CoutBomCalcule = CalculerCoutBomLocal(Lignes);
                if (SelectedProduitId.HasValue)
                {
                    BomTreeRoot = await ConstruireBomTreeAsync(SelectedProduitId.Value);
                }
                return Page();
            }

            var produitPrincipal = await _context.Produits
                .FirstOrDefaultAsync(p => p.Id == SelectedProduitId.Value);

            if (produitPrincipal == null)
            {
                ModelState.AddModelError(string.Empty, "Produit principal introuvable.");
                CoutBomCalcule = CalculerCoutBomLocal(Lignes);
                if (SelectedProduitId.HasValue)
                {
                    BomTreeRoot = await ConstruireBomTreeAsync(SelectedProduitId.Value);
                }
                return Page();
            }

            Bom bom;
            if (BomId.HasValue)
            {
                bom = await _context.Boms
                    .Include(b => b.Lignes)
                    .FirstOrDefaultAsync(b => b.Id == BomId.Value);

                if (bom == null)
                {
                    bom = new Bom
                    {
                        ProduitId = produitPrincipal.Id,
                        Lignes = new List<BomLigne>()
                    };
                    _context.Boms.Add(bom);
                }
                else
                {
                    _context.BomLignes.RemoveRange(bom.Lignes);
                    bom.Lignes.Clear();
                }
            }
            else
            {
                bom = await _context.Boms
                    .Include(b => b.Lignes)
                    .FirstOrDefaultAsync(b => b.ProduitId == produitPrincipal.Id);

                if (bom == null)
                {
                    bom = new Bom
                    {
                        ProduitId = produitPrincipal.Id,
                        Lignes = new List<BomLigne>()
                    };
                    _context.Boms.Add(bom);
                }
                else
                {
                    _context.BomLignes.RemoveRange(bom.Lignes);
                    bom.Lignes.Clear();
                }
            }

            var lignesValides = Lignes
                .Where(l => l.ComposantProduitId.HasValue)
                .ToList();

            foreach (var ligneInput in lignesValides)
            {
                var comp = await _context.Produits
                    .FirstOrDefaultAsync(p => p.Id == ligneInput.ComposantProduitId.Value);

                if (comp == null)
                    continue;

                var coutUnitaire = GetCoutUnitairePourBom(comp);

                var ligne = new BomLigne
                {
                    ComposantProduitId = comp.Id,
                    Quantite = ligneInput.Quantite ?? 0,
                    PrixUnitaire = coutUnitaire,
                    AutresCharges = ligneInput.AutresCharges ?? 0
                };

                bom.Lignes.Add(ligne);
            }

            await _context.SaveChangesAsync();

            BomId = bom.Id;

            var coutBom = await CalculerCoutBomPourProduitAsync(produitPrincipal.Id);
            produitPrincipal.CoutBom = coutBom;
            await _context.SaveChangesAsync();

            CoutBomCalcule = coutBom;
            BomTreeRoot = await ConstruireBomTreeAsync(produitPrincipal.Id);

            TempData["BomSaved"] = "La nomenclature a été enregistrée avec succès.";
            return RedirectToPage("/BOM");
        }

        private async Task ChargerProduitsAsync()
        {
            Produits = await _context.Produits
                .AsNoTracking()
                .Select(p => new ProduitViewModel
                {
                    Id = p.Id,
                    Nom = p.Nom,
                    Reference = p.Reference ?? string.Empty,
                    PrixVente = p.PrixVente,
                    CoutBom = p.CoutBom,
                    TypeTechnique = p.TypeTechnique,
                    CoutAchat = p.CoutAchat
                })
                .ToListAsync();
        }

        private decimal CalculerCoutBomLocal(List<BomLigneInput> lignes)
        {
            if (lignes == null || lignes.Count == 0)
                return 0m;

            decimal total = 0m;
            foreach (var l in lignes)
            {
                var prix = l.PrixUnitaire ?? 0m;
                var qte = l.Quantite ?? 0m;
                var chg = l.AutresCharges ?? 0m;
                total += prix * qte + chg;
            }
            return total;
        }

        private decimal GetCoutUnitairePourBom(Produit produit)
        {
            return produit.TypeTechnique == TypeTechniqueProduit.MatierePremiere
                ? produit.CoutAchat
                : produit.CoutBom;
        }

        private async Task<decimal> CalculerCoutBomPourProduitAsync(int produitId)
        {
            var bom = await _context.Boms
                .Include(b => b.Lignes)
                .ThenInclude(l => l.ComposantProduit)
                .FirstOrDefaultAsync(b => b.ProduitId == produitId);

            if (bom == null || bom.Lignes == null || bom.Lignes.Count == 0)
                return 0m;

            decimal total = 0m;
            foreach (var l in bom.Lignes)
            {
                var comp = l.ComposantProduit;
                if (comp == null)
                    continue;

                var coutUnitaire = GetCoutUnitairePourBom(comp);
                var qte = l.Quantite;
                var chg = l.AutresCharges;
                total += coutUnitaire * qte + chg;
            }

            return total;
        }

        public IHtmlContent RenderBomTree()
        {
            if (BomTreeRoot == null)
                return new HtmlString(string.Empty);

            var sb = new StringBuilder();
            RenderBomNodeInto(BomTreeRoot, 0, sb);
            return new HtmlString(sb.ToString());
        }

        private async Task<BomTreeNode?> ConstruireBomTreeAsync(int produitId, decimal quantiteParent = 1m, HashSet<int>? visited = null)
        {
            visited ??= new HashSet<int>();
            if (visited.Contains(produitId))
            {
                return null;
            }
            visited.Add(produitId);

            var produit = await _context.Produits
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == produitId);

            if (produit == null)
                return null;

            var node = new BomTreeNode
            {
                ProduitId = produit.Id,
                NomProduit = produit.Nom,
                Reference = produit.Reference ?? string.Empty,
                QuantiteTotale = quantiteParent
            };

            var bom = await _context.Boms
                .Include(b => b.Lignes)
                .FirstOrDefaultAsync(b => b.ProduitId == produitId);

            if (bom != null && bom.Lignes != null && bom.Lignes.Count > 0)
            {
                foreach (var ligne in bom.Lignes)
                {
                    var child = await ConstruireBomTreeAsync(ligne.ComposantProduitId, quantiteParent * ligne.Quantite, visited);
                    if (child != null)
                    {
                        node.Enfants.Add(child);
                    }
                }
            }

            visited.Remove(produitId);
            return node;
        }

        private void RenderBomNodeInto(BomTreeNode node, int level, StringBuilder sb)
        {
            sb.Append("<div class=\"bom-tree-row\">");
            sb.Append("<span class=\"bom-tree-qty\">");
            sb.Append(node.QuantiteTotale.ToString("0.##"));
            sb.Append("</span>");

            sb.Append("<span style=\"margin-right:6px; color:#64748b;\">");
            for (int i = 0; i < level; i++)
            {
                sb.Append("• ");
            }
            sb.Append("</span>");

            sb.Append("<span class=\"bom-tree-name\">");
            sb.Append(node.NomProduit);
            sb.Append("</span>");

            if (!string.IsNullOrEmpty(node.Reference))
            {
                sb.Append("<span class=\"bom-tree-ref\">(");
                sb.Append(node.Reference);
                sb.Append(")</span>");
            }

            sb.Append("</div>");

            if (node.Enfants != null && node.Enfants.Count > 0)
            {
                foreach (var child in node.Enfants)
                {
                    RenderBomNodeInto(child, level + 1, sb);
                }
            }
        }
    }
}
