using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages
{
    public class MRPDetailModel : PageModel
    {
        private readonly ErpDbContext _db;

        public MRPDetailModel(ErpDbContext db)
        {
            _db = db;
        }

        // ---------- VIEWMODELS POUR L'AFFICHAGE ----------

        public class PlanificationVm
        {
            public int Id { get; set; }
            public string Reference { get; set; } = string.Empty;
            public int HorizonJours { get; set; }
            public DateTime DateDebut { get; set; }
            public DateTime DateFin { get; set; }
            public string Statut { get; set; } = "Sauvegardée"; // Annulée / Sauvegardée / Terminée
        }

        public class LigneMrpVm
        {
            public int Niveau { get; set; }
            public string CodeArticle { get; set; } = string.Empty;
            public string LibelleArticle { get; set; } = string.Empty;
            public string TypeProduit { get; set; } = "PF";
            public string Unite { get; set; } = "PCS";

            public DateTime DateBesoin { get; set; }
            public decimal QuantiteBesoin { get; set; }
            public decimal StockDisponible { get; set; }
            public decimal QuantiteALancer { get; set; }
            public decimal Prix { get; set; }

            public bool EstOrdreFabrication =>
                TypeProduit == "PF" || TypeProduit == "SF" || TypeProduit == "PF+SF";

            public bool EstOrdreAchat => TypeProduit == "MP";
        }

        /// <summary>
        /// Une période dans le tableau MRP (ici 1 jour = 1 colonne).
        /// </summary>
        public class PeriodeMrpVm
        {
            public int Index { get; set; }          // 1, 2, 3, ...
            public DateTime Date { get; set; }      // Date de la période
            public string LabelCourt { get; set; } = "";   // ex : "P1", "P2" ou "01/03"
            public string LabelLong { get; set; } = "";    // ex : "01/03/2026"
        }

        /// <summary>
        /// Info BOM minimale envoyée au JS pour construire les tableaux du popup.
        /// </summary>
        public class BomInfoVm
        {
            public string CodeArticlePF { get; set; } = string.Empty;
            public string NomPF { get; set; } = string.Empty;
            public int ComponentCount { get; set; }  // nombre de composants dans la BOM
        }

        // ---------- PROPRIÉTÉS POUR LA VUE ----------

        public PlanificationVm? Planification { get; set; }
        public List<LigneMrpVm> Lignes { get; set; } = new List<LigneMrpVm>();

        // Périodes (colonnes du tableau MRP produit)
        public List<PeriodeMrpVm> Periodes { get; set; } = new List<PeriodeMrpVm>();

        // Infos BOM sérialisées pour le JS (popup)
        public string BomInfosJson { get; set; } = "[]";

        // Compteurs pour le bandeau
        public int NbProduitsPlanifies => Lignes.Count(l => l.Niveau == 0);
        public int NbPropositionsOF => Lignes.Count(l => l.EstOrdreFabrication && l.QuantiteALancer > 0);
        public int NbPropositionsOA => Lignes.Count(l => l.EstOrdreAchat && l.QuantiteALancer > 0);

        // ---------- HANDLER GET PRINCIPAL ----------

        public async Task<IActionResult> OnGetAsync(
            int? id,
            int? horizonJours,
            string? selectedProductIds)
        {
            if (id.HasValue)
            {
                var plan = await _db.MRPPlans
                    .Include(p => p.Lignes)
                        .ThenInclude(l => l.Produit)
                    .FirstOrDefaultAsync(p => p.Id == id.Value);

                if (plan == null)
                {
                    return RedirectToPage("/MRP");
                }

                await MapFromEntityAsync(plan);
                ConstruirePeriodesDepuisPlanification();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(selectedProductIds))
            {
                return RedirectToPage("/MRP");
            }

            var idsProduits = selectedProductIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s =>
                {
                    if (int.TryParse(s, out var pid)) return (int?)pid;
                    return null;
                })
                .Where(pid => pid.HasValue)
                .Select(pid => pid!.Value)
                .Distinct()
                .ToList();

            if (!idsProduits.Any())
            {
                return RedirectToPage("/MRP");
            }

            var horizon = horizonJours.GetValueOrDefault(30);
            if (horizon <= 0 || horizon > 365)
            {
                horizon = 30;
            }

            var newPlan = await CreerNouveauPlanAvecLignesAsync(idsProduits, horizon);

            await MapFromEntityAsync(newPlan);
            ConstruirePeriodesDepuisPlanification();

            return Page();
        }

        // ---------- MÉTHODES PRIVÉES (MAPPING & CREATION) ----------

        private async Task<MRPPlan> CreerNouveauPlanAvecLignesAsync(
            List<int> idsProduits,
            int horizonJours)
        {
            var dateDebut = DateTime.UtcNow.Date;
            var dateFin = dateDebut.AddDays(horizonJours);

            var refPlan = "MRP-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

            var plan = new MRPPlan
            {
                Reference = refPlan,
                DateCreation = DateTime.UtcNow,
                DateDebutHorizon = dateDebut,
                DateFinHorizon = dateFin,
                HorizonJours = horizonJours,
                Statut = "Brouillon",
                TypePlan = "Simulation"
            };

            var produits = await _db.Produits
                .Where(p => idsProduits.Contains(p.Id))
                .ToListAsync();

            foreach (var prod in produits)
            {
                var ligne = new MRPPlanLigne
                {
                    ProduitId = prod.Id,
                    DateBesoin = dateFin,
                    QuantiteBesoin = 0,
                    StockDisponible = prod.QuantiteDisponible,
                    QuantiteALancer = 0,
                    TypeProduit = "Fini"
                };

                plan.Lignes.Add(ligne);
            }

            _db.MRPPlans.Add(plan);
            await _db.SaveChangesAsync();

            return plan;
        }

        private async Task MapFromEntityAsync(MRPPlan plan)
        {
            Planification = new PlanificationVm
            {
                Id = plan.Id,
                Reference = plan.Reference,
                HorizonJours = plan.HorizonJours,
                DateDebut = plan.DateDebutHorizon,
                DateFin = plan.DateFinHorizon,
                Statut = plan.Statut
            };

            Lignes = new List<LigneMrpVm>();

            // Lignes niveau 0 (produits planifiés)
            foreach (var l in plan.Lignes)
            {
                var prod = l.Produit;

                var vm = new LigneMrpVm
                {
                    Niveau = 0,
                    CodeArticle = prod?.Reference ?? ("PROD-" + l.ProduitId),
                    LibelleArticle = prod?.Nom ?? "Produit " + l.ProduitId,
                    TypeProduit = "PF",
                    Unite = "PCS", // pas de propriété Unite dans Produit, on fixe à PCS
                    DateBesoin = l.DateBesoin,
                    QuantiteBesoin = l.QuantiteBesoin,
                    StockDisponible = l.StockDisponible,
                    QuantiteALancer = l.QuantiteALancer,
                    Prix = prod?.PrixVente ?? 0m
                };

                Lignes.Add(vm);
            }

            // Construire les infos BOM (pour le popup)
            await ConstruireInfosBomAsync();
        }

        /// <summary>
        /// Construit la liste des périodes (1 jour = 1 colonne) sur l'horizon.
        /// </summary>
        private void ConstruirePeriodesDepuisPlanification()
        {
            Periodes = new List<PeriodeMrpVm>();

            if (Planification == null)
                return;

            var debut = Planification.DateDebut;
            var horizon = Planification.HorizonJours;

            if (horizon <= 0) horizon = 1;

            for (int i = 0; i < horizon; i++)
            {
                var date = debut.AddDays(i);
                Periodes.Add(new PeriodeMrpVm
                {
                    Index = i + 1,
                    Date = date,
                    LabelCourt = $"P{i + 1}",
                    LabelLong = date.ToString("dd/MM/yyyy")
                });
            }
        }

        /// <summary>
        /// Pour chaque produit fini planifié (niveau 0), récupère le nombre de composants dans sa BOM
        /// et sérialise ça en JSON pour le JS du popup.
        /// </summary>
        private async Task ConstruireInfosBomAsync()
        {
            var infos = new List<BomInfoVm>();

            // Codes articles PF (niveau 0)
            var codesPf = Lignes
                .Where(l => l.Niveau == 0)
                .Select(l => l.CodeArticle)
                .Distinct()
                .ToList();

            if (!codesPf.Any())
            {
                BomInfosJson = "[]";
                return;
            }

            // Récupérer les produits associés
            var produitsPf = await _db.Produits
                .Where(p => codesPf.Contains(p.Reference))
                .ToListAsync();

            var idsPf = produitsPf.Select(p => p.Id).ToList();

            // Charger les BOM pour ces produits
            var boms = await _db.Boms
                .Include(b => b.Lignes)
                .Where(b => idsPf.Contains(b.ProduitId))
                .ToListAsync();

            foreach (var prod in produitsPf)
            {
                var bom = boms.FirstOrDefault(b => b.ProduitId == prod.Id);
                var count = bom?.Lignes?.Count ?? 0;

                if (count < 0) count = 0;

                infos.Add(new BomInfoVm
                {
                    CodeArticlePF = prod.Reference,
                    NomPF = prod.Nom,
                    ComponentCount = count
                });
            }

            BomInfosJson = JsonSerializer.Serialize(infos);
        }

        // ---------- LOGIQUE UI EXISTANTE ----------

        public bool PlanifAutoriseLancement()
        {
            if (Planification == null) return false;
            return string.Equals(Planification.Statut, "Sauvegardée", StringComparison.OrdinalIgnoreCase);
        }

        public string GetStatutBadgeCss()
        {
            if (Planification == null) return "badge-status badge-neutral";

            return Planification.Statut switch
            {
                "Annulee" => "badge-status badge-annulee",
                "Annulée" => "badge-status badge-annulee",
                "Terminee" => "badge-status badge-terminee",
                "Terminée" => "badge-status badge-terminee",
                "Sauvegardée" => "badge-status badge-sauvegardee",
                _ => "badge-status badge-neutral"
            };
        }

        public IActionResult OnPostLogout()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/BDDView");
        }
    }
}
