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

        // VIEWMODELS POUR L AFFICHAGE
        public class PlanificationVm
        {
            public int Id { get; set; }
            public string Reference { get; set; } = string.Empty;
            public int HorizonJours { get; set; }
            public DateTime DateDebut { get; set; }
            public DateTime DateFin { get; set; }
            public string Statut { get; set; } = "Brouillon";
        }

        public class LigneMrpVm
        {
            public int Niveau { get; set; }
            public string CodeArticle { get; set; } = string.Empty;
            public string? ParentCodeArticle { get; set; }
            public string LibelleArticle { get; set; } = string.Empty;
            public string TypeProduit { get; set; } = "PF"; // PF / SF / MP / PF+SF
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

        public class PeriodeMrpVm
        {
            public int Index { get; set; }
            public DateTime Date { get; set; }
            public string LabelCourt { get; set; } = "";
            public string LabelLong { get; set; } = "";
        }

        public class BomInfoVm
        {
            public string CodeArticlePF { get; set; } = string.Empty;
            public string NomPF { get; set; } = string.Empty;
            public int ComponentCount { get; set; }
        }

        public class StockMrpVm
        {
            public string CodeArticle { get; set; } = string.Empty;
            public decimal StockDisponible { get; set; }
        }

        public class BomDetailVm
        {
            public string CodeArticlePF { get; set; } = string.Empty;
            public List<BomDetailComponentVm> Composants { get; set; } = new();
        }

        public class BomDetailComponentVm
        {
            public string CodeArticle { get; set; } = string.Empty;
            public string Nom { get; set; } = string.Empty;
        }

        // PROPRIETES POUR LA VUE
        public PlanificationVm? Planification { get; set; }
        public List<LigneMrpVm> Lignes { get; set; } = new List<LigneMrpVm>();
        public List<PeriodeMrpVm> Periodes { get; set; } = new List<PeriodeMrpVm>();

        public string BomInfosJson { get; set; } = "[]";
        public string StockMrpJson { get; set; } = "[]";
        public string BomDetailsJson { get; set; } = "[]";

        public int NbProduitsPlanifies => Lignes.Count(l => l.Niveau == 0);
        public int NbPropositionsOF => Lignes.Count(l => l.EstOrdreFabrication && l.QuantiteALancer > 0);
        public int NbPropositionsOA => Lignes.Count(l => l.EstOrdreAchat && l.QuantiteALancer > 0);

        // HANDLER GET PRINCIPAL
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

        // HANDLERS POST
        public async Task<IActionResult> OnPostEnregistrerAsync(int planId)
        {
            return await ModifierStatutPlanAsync(planId, "Sauvegardee");
        }

        public async Task<IActionResult> OnPostAnnulerAsync(int planId)
        {
            return await ModifierStatutPlanAsync(planId, "Annulee");
        }

        private async Task<IActionResult> ModifierStatutPlanAsync(int planId, string nouveauStatut)
        {
            var plan = await _db.MRPPlans.FirstOrDefaultAsync(p => p.Id == planId);

            if (plan == null)
            {
                TempData["Erreur"] = "Planification introuvable.";
                return RedirectToPage("/MRP");
            }

            if (plan.Statut == "Terminee")
            {
                TempData["Erreur"] = "Impossible de modifier une planification terminee.";
                return RedirectToPage("/MRP");
            }

            plan.Statut = nouveauStatut;

            await _db.SaveChangesAsync();

            TempData["Succes"] = $"Planification {nouveauStatut.ToLower()} avec succes.";
            return RedirectToPage("/MRP");
        }

        // CREATION DU PLAN
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

        private string MapTypeTechniqueToMrpType(TypeTechniqueProduit typeTech)
        {
            return typeTech switch
            {
                TypeTechniqueProduit.MatierePremiere => "MP",
                TypeTechniqueProduit.SemiFini => "SF",
                TypeTechniqueProduit.Fini => "PF",
                TypeTechniqueProduit.SemiFiniEtFini => "PF+SF",
                _ => "PF"
            };
        }

        // MAPPING ENTITE -> VIEWMODEL
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

            var produitIds = plan.Lignes.Select(l => l.ProduitId).Distinct().ToList();

            var produits = await _db.Produits
                .Where(p => produitIds.Contains(p.Id))
                .ToListAsync();

            var planProduitsDict = produits.ToDictionary(p => p.Id, p => p);

            var bomsCache = await _db.Boms
                .Include(b => b.Lignes)
                    .ThenInclude(bl => bl.ComposantProduit)
                .ToListAsync();

            foreach (var l in plan.Lignes)
            {
                if (!planProduitsDict.TryGetValue(l.ProduitId, out var prod))
                    continue;

                var codePf = prod.Reference;
                var libPf = prod.Nom;
                var typePf = MapTypeTechniqueToMrpType(prod.TypeTechnique);
                var stockPfActuel = prod.QuantiteDisponible;

                var vmPf = new LigneMrpVm
                {
                    Niveau = 0,
                    CodeArticle = codePf,
                    ParentCodeArticle = null,
                    LibelleArticle = libPf,
                    TypeProduit = typePf,
                    Unite = "PCS",
                    DateBesoin = l.DateBesoin,
                    QuantiteBesoin = l.QuantiteBesoin,
                    StockDisponible = stockPfActuel,
                    QuantiteALancer = l.QuantiteALancer,
                    Prix = prod.PrixVente
                };

                Lignes.Add(vmPf);

                await AjouterComposantsRecursifsAsync(
                    produitParent: prod,
                    niveauParent: 0,
                    codeParent: codePf,
                    dateBesoin: l.DateBesoin,
                    bomsCache: bomsCache);
            }

            await ConstruireInfosBomAsync();
            ConstruireStockMrp();
            ConstruireBomDetails();
        }

        // Ajoute recursivement les composants (SF/MP) d un produit parent
        private async Task AjouterComposantsRecursifsAsync(
            Produit produitParent,
            int niveauParent,
            string codeParent,
            DateTime dateBesoin,
            List<Bom> bomsCache)
        {
            var bom = bomsCache.FirstOrDefault(b => b.ProduitId == produitParent.Id);
            if (bom == null || bom.Lignes == null || bom.Lignes.Count == 0)
                return;

            foreach (var bl in bom.Lignes)
            {
                var comp = bl.ComposantProduit;
                if (comp == null)
                {
                    comp = await _db.Produits.FirstOrDefaultAsync(p => p.Id == bl.ComposantProduitId);
                    if (comp == null) continue;
                }

                var codeComp = comp.Reference;
                var libComp = comp.Nom;
                var typeComp = MapTypeTechniqueToMrpType(comp.TypeTechnique);

                var vmComp = new LigneMrpVm
                {
                    Niveau = niveauParent + 1,
                    CodeArticle = codeComp,
                    ParentCodeArticle = codeParent,
                    LibelleArticle = libComp,
                    TypeProduit = typeComp,
                    Unite = "PCS",
                    DateBesoin = dateBesoin,
                    QuantiteBesoin = 0,
                    StockDisponible = comp.QuantiteDisponible,
                    QuantiteALancer = 0,
                    Prix = comp.PrixVente
                };

                Lignes.Add(vmComp);

                if (typeComp == "SF" || typeComp == "PF+SF")
                {
                    await AjouterComposantsRecursifsAsync(
                        produitParent: comp,
                        niveauParent: niveauParent + 1,
                        codeParent: codeComp,
                        dateBesoin: dateBesoin,
                        bomsCache: bomsCache);
                }
            }
        }

        // PERIODES
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

        // INFOS BOM POUR POPUP
        private async Task ConstruireInfosBomAsync()
        {
            var infos = new List<BomInfoVm>();

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

            var produitsPf = await _db.Produits
                .Where(p => codesPf.Contains(p.Reference))
                .ToListAsync();

            foreach (var prod in produitsPf)
            {
                var codePf = prod.Reference;

                var composantsDistincts = Lignes
                    .Where(l => l.CodeArticle != codePf)
                    .Select(l => l.CodeArticle)
                    .Distinct()
                    .Count();

                infos.Add(new BomInfoVm
                {
                    CodeArticlePF = codePf,
                    NomPF = prod.Nom,
                    ComponentCount = composantsDistincts
                });
            }

            BomInfosJson = JsonSerializer.Serialize(infos);
        }

        private void ConstruireStockMrp()
        {
            var stocks = Lignes
                .GroupBy(l => l.CodeArticle)
                .Select(g => new StockMrpVm
                {
                    CodeArticle = g.Key,
                    StockDisponible = g.First().StockDisponible
                })
                .ToList();

            StockMrpJson = JsonSerializer.Serialize(stocks);
        }

        private void ConstruireBomDetails()
        {
            var pfCodes = Lignes
                .Where(l => l.Niveau == 0)
                .Select(l => l.CodeArticle)
                .Distinct()
                .ToList();

            var details = new List<BomDetailVm>();

            foreach (var codePf in pfCodes)
            {
                var pf = new BomDetailVm
                {
                    CodeArticlePF = codePf
                };

                var composants = Lignes
                    .Where(l => l.CodeArticle != codePf)
                    .Select(l => new BomDetailComponentVm
                    {
                        CodeArticle = l.CodeArticle,
                        Nom = l.LibelleArticle
                    })
                    .DistinctBy(c => c.CodeArticle)
                    .ToList();

                pf.Composants = composants;
                details.Add(pf);
            }

            BomDetailsJson = JsonSerializer.Serialize(details);
        }

        // LOGIQUE UI EXISTANTE
        public bool PlanifAutoriseLancement()
        {
            if (Planification == null) return false;
            return string.Equals(Planification.Statut, "Sauvegardee", StringComparison.OrdinalIgnoreCase);
        }

        public string GetStatutBadgeCss()
        {
            if (Planification == null) return "badge-status badge-neutral";

            return Planification.Statut switch
            {
                "Annulee" => "badge-status badge-annulee",
                "Terminee" => "badge-status badge-terminee",
                "Sauvegardee" => "badge-status badge-sauvegardee",
                _ => "badge-status badge-neutral"
            };
        }

        public IActionResult OnPostLogout()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/BDDView");
        }
    }

    public static class EnumerableExtensions
    {
        public static IEnumerable<T> DistinctBy<T, TKey>(
            this IEnumerable<T> source,
            Func<T, TKey> keySelector)
        {
            var seen = new HashSet<TKey>();
            foreach (var element in source)
            {
                if (seen.Add(keySelector(element)))
                    yield return element;
            }
        }
    }
}
