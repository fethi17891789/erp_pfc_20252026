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

        public class ProduitViewModel
        {
            public int Id { get; set; }
            public string Nom { get; set; } = string.Empty;
            public string Reference { get; set; } = string.Empty;
            public decimal PrixVente { get; set; }
        }

        public class BomLigneInput
        {
            public int Index { get; set; }
            public int? ComposantProduitId { get; set; }
            public string? ComposantNomSaisi { get; set; }
            public decimal PrixUnitaire { get; set; }
            public decimal Quantite { get; set; }
        }

        // ====== modèle d'arbre ======
        public class BomTreeNode
        {
            public int ProduitId { get; set; }
            public string NomProduit { get; set; } = string.Empty;
            public string Reference { get; set; } = string.Empty;
            public decimal QuantiteTotale { get; set; }
            public List<BomTreeNode> Enfants { get; set; } = new();
        }

        public BomTreeNode? BomTreeRoot { get; set; }

        // fromProductId : produit principal, fromComponentId+rowIndex : ligne composant
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
                                Quantite = l.Quantite
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
                        PrixUnitaire = 0
                    });
                }

                var comp = await _context.Produits
                    .Where(p => p.Id == fromComponentId.Value)
                    .Select(p => new ProduitViewModel
                    {
                        Id = p.Id,
                        Nom = p.Nom,
                        Reference = p.Reference ?? string.Empty,
                        PrixVente = p.PrixVente
                    })
                    .FirstOrDefaultAsync();

                if (comp != null)
                {
                    var ligne = Lignes[rowIndex.Value];
                    ligne.ComposantProduitId = comp.Id;
                    ligne.ComposantNomSaisi = string.IsNullOrEmpty(comp.Reference)
                        ? comp.Nom
                        : $"{comp.Nom} ({comp.Reference})";
                    ligne.PrixUnitaire = comp.PrixVente;
                    if (ligne.Quantite <= 0) ligne.Quantite = 1;
                }
            }

            if (SelectedProduitId.HasValue)
                BomTreeRoot = await BuildBomTreeAsync(SelectedProduitId.Value, 1m, new HashSet<int>());
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await ChargerProduitsAsync();

            if (SelectedProduitId == null)
            {
                ModelState.AddModelError(nameof(SelectedProduitId),
                    "Vous devez sélectionner un produit existant pour créer une nomenclature.");
            }
            else
            {
                var existe = await _context.Produits
                    .AnyAsync(p => p.Id == SelectedProduitId.Value);

                if (!existe)
                {
                    ModelState.AddModelError(nameof(SelectedProduitId),
                        "Le produit sélectionné n'existe plus dans la base.");
                }
            }

            Lignes ??= new List<BomLigneInput>();

            var lignesValides = Lignes
                .Where(l =>
                    l.ComposantProduitId.HasValue &&
                    l.ComposantProduitId.Value > 0 &&
                    l.Quantite > 0)
                .ToList();

            if (!lignesValides.Any())
            {
                ModelState.AddModelError(string.Empty,
                    "Vous devez ajouter au moins une ligne composant avec une quantité > 0.");
            }

            if (SelectedProduitId.HasValue &&
                lignesValides.Any(l => l.ComposantProduitId == SelectedProduitId.Value))
            {
                ModelState.AddModelError(string.Empty,
                    "Un produit ne peut pas être son propre composant.");
            }

            if (!ModelState.IsValid)
            {
                if (SelectedProduitId.HasValue)
                    BomTreeRoot = await BuildBomTreeAsync(SelectedProduitId.Value, 1m, new HashSet<int>());

                return Page();
            }

            Bom? bom;

            if (BomId.HasValue)
            {
                bom = await _context.Boms
                    .Include(b => b.Lignes)
                    .FirstOrDefaultAsync(b => b.Id == BomId.Value);

                if (bom == null)
                {
                    bom = new Bom
                    {
                        ProduitId = SelectedProduitId!.Value,
                        Lignes = new List<BomLigne>()
                    };
                    _context.Boms.Add(bom);
                }
                else
                {
                    _context.BomLignes.RemoveRange(bom.Lignes);
                    bom.Lignes.Clear();
                    bom.ProduitId = SelectedProduitId!.Value;
                }
            }
            else
            {
                bom = new Bom
                {
                    ProduitId = SelectedProduitId!.Value,
                    Lignes = new List<BomLigne>()
                };
                _context.Boms.Add(bom);
            }

            bom.Lignes = lignesValides.Select(l => new BomLigne
            {
                ComposantProduitId = l.ComposantProduitId!.Value,
                Quantite = l.Quantite,
                PrixUnitaire = l.PrixUnitaire
            }).ToList();

            await _context.SaveChangesAsync();

            return RedirectToPage("/Fabrication/BOM");
        }

        public IActionResult OnPostLogout()
        {
            return RedirectToPage("/Index");
        }

        private async Task ChargerProduitsAsync()
        {
            Produits = await _context.Produits
                .OrderBy(p => p.Nom)
                .Select(p => new ProduitViewModel
                {
                    Id = p.Id,
                    Nom = p.Nom,
                    Reference = p.Reference ?? string.Empty,
                    PrixVente = p.PrixVente
                })
                .ToListAsync();
        }

        // ====== construction récursive de l'arbre ======
        private async Task<BomTreeNode> BuildBomTreeAsync(int produitId, decimal quantite, HashSet<int> visited)
        {
            if (visited.Contains(produitId))
            {
                return new BomTreeNode
                {
                    ProduitId = produitId,
                    NomProduit = "[BOM récursive détectée]",
                    Reference = "",
                    QuantiteTotale = quantite,
                    Enfants = new List<BomTreeNode>()
                };
            }

            visited.Add(produitId);

            var produit = await _context.Produits
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == produitId);

            var node = new BomTreeNode
            {
                ProduitId = produitId,
                NomProduit = produit?.Nom ?? $"Produit #{produitId}",
                Reference = produit?.Reference ?? string.Empty,
                QuantiteTotale = quantite,
                Enfants = new List<BomTreeNode>()
            };

            var bom = await _context.Boms
                .Include(b => b.Lignes)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.ProduitId == produitId);

            if (bom != null && bom.Lignes.Any())
            {
                foreach (var ligne in bom.Lignes)
                {
                    var qteCumul = quantite * ligne.Quantite;
                    var child = await BuildBomTreeAsync(ligne.ComposantProduitId, qteCumul, visited);
                    node.Enfants.Add(child);
                }
            }

            visited.Remove(produitId);
            return node;
        }

        // ====== rendu HTML de l'arbre pour la vue ======
        public IHtmlContent RenderBomTree()
        {
            if (BomTreeRoot == null)
                return HtmlString.Empty;

            var sb = new StringBuilder();
            RenderBomNodeInto(BomTreeRoot, 0, sb);
            return new HtmlString(sb.ToString());
        }

        private void RenderBomNodeInto(BomTreeNode node, int level, StringBuilder sb)
        {
            var marginLeft = level * 18;
            var nom = System.Net.WebUtility.HtmlEncode(node.NomProduit ?? "");
            var reference = System.Net.WebUtility.HtmlEncode(node.Reference ?? "");

            sb.Append($@"
<div class=""bom-tree-row"" style=""margin-left:{marginLeft}px;"">
  <span class=""bom-tree-qty"">{node.QuantiteTotale} x</span>
  <span class=""bom-tree-name"">{nom}");

            if (!string.IsNullOrEmpty(reference))
            {
                sb.Append($@" <span class=""bom-tree-ref"">({reference})</span>");
            }

            sb.Append("</span></div>");

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
