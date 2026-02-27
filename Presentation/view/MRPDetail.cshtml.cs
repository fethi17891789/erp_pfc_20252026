// Fichier : Presentation/view/MRPDetail.cshtml.cs
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
using erp_pfc_20252026.Data.Entities;

namespace erp_pfc_20252026.Pages
{
    public class MRPDetailModel : PageModel
    {
        private readonly ErpDbContext _db;
        private readonly OrdreFabricationService _ofService;

        public MRPDetailModel(ErpDbContext db, OrdreFabricationService ofService)
        {
            _db = db;
            _ofService = ofService;
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
            public string TypeProduit { get; set; } = "PF";
            public string Unite { get; set; } = "PCS";

            public DateTime DateBesoin { get; set; }
            public decimal QuantiteBesoin { get; set; }
            public decimal StockDisponible { get; set; }

            // Qte a lancer finale (somme des DEB) pour cette ligne
            public decimal QuantiteALancer { get; set; }

            // Prix total = QuantiteALancer * CoutBom (pour OF et SF)
            public decimal Prix { get; set; }

            public decimal QuantiteParParent { get; set; } = 1m;

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
            public decimal QuantiteParParent { get; set; }
        }

        public class BomRatioVm
        {
            public string ParentCodeArticle { get; set; } = string.Empty;
            public string EnfantCodeArticle { get; set; } = string.Empty;
            public decimal QuantiteParParent { get; set; }
        }

        // DTO pour sauvegarde / chargement MRPTableau
        public class MrpTableauDto
        {
            public int PlanId { get; set; }
            public string CodeArticle { get; set; } = string.Empty;
            public int NumeroPeriode { get; set; }
            public DateTime DatePeriode { get; set; }
            public decimal BesoinBrut { get; set; }
            public decimal StockPrevisionnel { get; set; }
            public decimal BesoinNet { get; set; }
            public decimal FinOrdre { get; set; }
            public decimal DebutOrdre { get; set; }
            public int DelaiJours { get; set; }
        }

        public PlanificationVm? Planification { get; set; }
        public List<LigneMrpVm> Lignes { get; set; } = new List<LigneMrpVm>();
        public List<PeriodeMrpVm> Periodes { get; set; } = new List<PeriodeMrpVm>();

        public string BomInfosJson { get; set; } = "[]";
        public string StockMrpJson { get; set; } = "[]";
        public string BomDetailsJson { get; set; } = "[]";
        public string BomRatiosJson { get; set; } = "[]";

        public int NbProduitsPlanifies => Lignes.Count(l => l.Niveau == 0);
        public int NbPropositionsOF => Lignes.Count(l => l.EstOrdreFabrication && l.QuantiteALancer > 0);
        public int NbPropositionsOA => Lignes.Count(l => l.EstOrdreAchat && l.QuantiteALancer > 0);

        // Liste des fichiers d'ordres de fabrication pour la planification
        public List<MRPFichier> FichiersOF { get; set; } = new List<MRPFichier>();

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
                await ChargerFichiersOFAsync(plan.Id);
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
            await ChargerFichiersOFAsync(newPlan.Id);

            return Page();
        }

        // HANDLERS POST EXISTANTS
        public async Task<IActionResult> OnPostEnregistrerAsync(int planId)
        {
            return await ModifierStatutPlanAsync(planId, "Sauvegardee");
        }

        public async Task<IActionResult> OnPostAnnulerAsync(int planId)
        {
            return await ModifierStatutPlanAsync(planId, "Annulee");
        }

        public async Task<IActionResult> OnPostLancerOFAsync(int planId, string codeArticle, decimal quantite)
        {
            if (planId <= 0 || string.IsNullOrWhiteSpace(codeArticle) || quantite <= 0)
            {
                TempData["Erreur"] = "Paramètres de lancement d'OF invalides.";
                return RedirectToPage(new { id = planId });
            }

            try
            {
                var fichier = await _ofService.GenererOrdreFabricationAsync(planId, codeArticle, quantite);
                TempData["Succes"] = $"Ordre de fabrication {fichier.ReferenceOF} généré pour l'article {codeArticle}.";
            }
            catch (Exception ex)
            {
                TempData["Erreur"] = "Erreur lors de la génération de l'ordre de fabrication : " + ex.Message;
            }

            return RedirectToPage(new { id = planId });
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

        // HANDLER JSON : charger tableaux sauvegardes pour un plan + article
        public async Task<IActionResult> OnGetLoadMrpTableauxAsync(int planId, string codeArticle)
        {
            if (string.IsNullOrWhiteSpace(codeArticle))
                return new JsonResult(Array.Empty<MrpTableauDto>());

            var plan = await _db.MRPPlans
                .Include(p => p.Lignes)
                    .ThenInclude(l => l.Produit)
                .FirstOrDefaultAsync(p => p.Id == planId);

            if (plan == null)
                return new JsonResult(Array.Empty<MrpTableauDto>());

            var ligne = plan.Lignes.FirstOrDefault(l => l.Produit.Reference == codeArticle);
            if (ligne == null)
                return new JsonResult(Array.Empty<MrpTableauDto>());

            var tableaux = await _db.MRPTables
                .Where(t => t.MRPPlanLigneId == ligne.Id)
                .OrderBy(t => t.NumeroPeriode)
                .ToListAsync();

            var dtos = tableaux.Select(t => new MrpTableauDto
            {
                PlanId = planId,
                CodeArticle = codeArticle,
                NumeroPeriode = t.NumeroPeriode,
                DatePeriode = t.DatePeriode,
                BesoinBrut = t.BesoinBrut,
                StockPrevisionnel = t.StockPrevisionnel,
                BesoinNet = t.BesoinNet,
                FinOrdre = t.FinOrdre,
                DebutOrdre = t.DebutOrdre,
                DelaiJours = t.DelaiJours
            }).ToList();

            return new JsonResult(dtos);
        }

        // HANDLER JSON : sauvegarder tableaux pour un plan
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSaveMrpTableauxAsync()
        {
            using var reader = new System.IO.StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            Console.WriteLine("MRP Save - Raw body: " + body);

            if (string.IsNullOrWhiteSpace(body))
                return new JsonResult(new { ok = false, message = "Corps vide." });

            var dtos = new List<MrpTableauDto>();

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return new JsonResult(new { ok = false, message = "JSON attendu: tableau." });
                }

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var dto = new MrpTableauDto();

                    if (el.TryGetProperty("planId", out var pPlanId) && pPlanId.ValueKind == JsonValueKind.Number)
                        dto.PlanId = pPlanId.GetInt32();

                    if (el.TryGetProperty("codeArticle", out var pCode) && pCode.ValueKind == JsonValueKind.String)
                        dto.CodeArticle = pCode.GetString() ?? string.Empty;

                    if (el.TryGetProperty("numeroPeriode", out var pNum) && pNum.ValueKind == JsonValueKind.Number)
                        dto.NumeroPeriode = pNum.GetInt32();

                    if (el.TryGetProperty("datePeriode", out var pDate) && pDate.ValueKind == JsonValueKind.String)
                    {
                        if (DateTime.TryParse(pDate.GetString(), out var d))
                            dto.DatePeriode = d;
                    }

                    if (el.TryGetProperty("besoinBrut", out var pBB) && pBB.ValueKind == JsonValueKind.Number)
                        dto.BesoinBrut = pBB.GetDecimal();

                    if (el.TryGetProperty("stockPrevisionnel", out var pSP) && pSP.ValueKind == JsonValueKind.Number)
                        dto.StockPrevisionnel = pSP.GetDecimal();

                    if (el.TryGetProperty("besoinNet", out var pBN) && pBN.ValueKind == JsonValueKind.Number)
                        dto.BesoinNet = pBN.GetDecimal();

                    if (el.TryGetProperty("finOrdre", out var pFIN) && pFIN.ValueKind == JsonValueKind.Number)
                        dto.FinOrdre = pFIN.GetDecimal();

                    if (el.TryGetProperty("debutOrdre", out var pDEB) && pDEB.ValueKind == JsonValueKind.Number)
                        dto.DebutOrdre = pDEB.GetDecimal();

                    if (el.TryGetProperty("delaiJours", out var pDEL) && pDEL.ValueKind == JsonValueKind.Number)
                        dto.DelaiJours = pDEL.GetInt32();

                    dtos.Add(dto);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("MRP Save - Exception parse JSON: " + ex);
                return new JsonResult(new { ok = false, message = "JSON invalide." });
            }

            Console.WriteLine("MRP Save - dtos count = " + dtos.Count);

            if (dtos.Count == 0)
                return new JsonResult(new { ok = false, message = "Aucune donnee recue." });

            var planId = dtos[0].PlanId;
            var plan = await _db.MRPPlans
                .Include(p => p.Lignes)
                    .ThenInclude(l => l.Produit)
                .FirstOrDefaultAsync(p => p.Id == planId);

            if (plan == null)
                return new JsonResult(new { ok = false, message = "Plan introuvable." });

            // Regroupement par article (code produit)
            var dtosParArticle = dtos
                .GroupBy(d => d.CodeArticle)
                .ToList();

            foreach (var grp in dtosParArticle)
            {
                var codeArticle = grp.Key;
                var ligne = plan.Lignes.FirstOrDefault(l => l.Produit.Reference == codeArticle);
                if (ligne == null)
                    continue;

                // 1) on supprime les anciennes lignes MRPTableau pour cette ligne
                var existants = await _db.MRPTables
                    .Where(t => t.MRPPlanLigneId == ligne.Id)
                    .ToListAsync();

                _db.MRPTables.RemoveRange(existants);

                // 2) on reinsere les nouvelles
                foreach (var dto in grp)
                {
                    var ent = new MRPTableau
                    {
                        MRPPlanLigneId = ligne.Id,
                        NumeroPeriode = dto.NumeroPeriode,
                        DatePeriode = dto.DatePeriode == default
                            ? plan.DateDebutHorizon.AddDays(dto.NumeroPeriode)
                            : dto.DatePeriode,
                        BesoinBrut = dto.BesoinBrut,
                        StockPrevisionnel = dto.StockPrevisionnel,
                        BesoinNet = dto.BesoinNet,
                        FinOrdre = dto.FinOrdre,
                        DebutOrdre = dto.DebutOrdre,
                        DelaiJours = dto.DelaiJours
                    };
                    _db.MRPTables.Add(ent);
                }
            }

            // 3) calcul QuantiteALancer = somme des DEB et PrixTotal = QuantiteALancer * CoutBom
            var lignesIds = plan.Lignes.Select(l => l.Id).ToList();
            var tableauxParLigne = await _db.MRPTables
                .Where(t => lignesIds.Contains(t.MRPPlanLigneId))
                .GroupBy(t => t.MRPPlanLigneId)
                .ToListAsync();

            foreach (var grp in tableauxParLigne)
            {
                var ligne = plan.Lignes.FirstOrDefault(l => l.Id == grp.Key);
                if (ligne == null) continue;

                var sommeDebutOrdre = grp.Sum(t => t.DebutOrdre);

                ligne.QuantiteALancer = sommeDebutOrdre;

                var prod = ligne.Produit;
                if (prod != null)
                {
                    // Prix total base sur CoutBom du produit
                    ligne.PrixTotal = sommeDebutOrdre * prod.CoutBom;
                }
            }

            await _db.SaveChangesAsync();
            return new JsonResult(new { ok = true });
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

            // Lignes pour TOUS les produits selectionnes (PF, SF, MP)
            foreach (var prod in produits)
            {
                var ligne = new MRPPlanLigne
                {
                    ProduitId = prod.Id,
                    DateBesoin = dateFin,
                    QuantiteBesoin = 0,
                    StockDisponible = prod.QuantiteDisponible,
                    QuantiteALancer = 0,
                    PrixTotal = 0m,
                    TypeProduit = "Fini"
                };

                plan.Lignes.Add(ligne);
            }

            // Lignes pour les composants (PF + SF + MP), pour le backend MRP
            await AjouterLignesComposantsPourPlanAsync(plan);

            _db.MRPPlans.Add(plan);
            await _db.SaveChangesAsync();

            return plan;
        }

        // Cree des MRPPlanLigne pour tous les composants d un plan
        private async Task AjouterLignesComposantsPourPlanAsync(MRPPlan plan)
        {
            var produitIds = plan.Lignes.Select(l => l.ProduitId).Distinct().ToList();

            var produits = await _db.Produits
                .Where(p => produitIds.Contains(p.Id))
                .ToListAsync();

            var produitsDict = produits.ToDictionary(p => p.Id, p => p);

            var boms = await _db.Boms
                .Include(b => b.Lignes)
                    .ThenInclude(bl => bl.ComposantProduit)
                .ToListAsync();

            var lignesExistantes = plan.Lignes
                .Select(l => l.ProduitId)
                .ToHashSet();

            foreach (var lignePf in plan.Lignes.ToList())
            {
                if (!produitsDict.TryGetValue(lignePf.ProduitId, out var prodPf))
                    continue;

                await CreerLignesComposantsRecursifsAsync(
                    plan,
                    prodPf,
                    lignesExistantes,
                    boms);
            }
        }

        private async Task CreerLignesComposantsRecursifsAsync(
            MRPPlan plan,
            Produit produitParent,
            HashSet<int> lignesExistantes,
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

                if (!lignesExistantes.Contains(comp.Id))
                {
                    var ligneComp = new MRPPlanLigne
                    {
                        ProduitId = comp.Id,
                        DateBesoin = plan.DateFinHorizon,
                        QuantiteBesoin = 0m,
                        StockDisponible = comp.QuantiteDisponible,
                        QuantiteALancer = 0m,
                        PrixTotal = 0m,
                        TypeProduit = MapTypeTechniqueToMrpType(comp.TypeTechnique)
                    };

                    plan.Lignes.Add(ligneComp);
                    lignesExistantes.Add(comp.Id);
                }

                if (comp.TypeTechnique == TypeTechniqueProduit.SemiFini ||
                    comp.TypeTechnique == TypeTechniqueProduit.SemiFiniEtFini)
                {
                    await CreerLignesComposantsRecursifsAsync(
                        plan,
                        comp,
                        lignesExistantes,
                        bomsCache);
                }
            }
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

            // dictionnaire des lignes de plan par ProduitId
            var lignesPlanParProduitId = plan.Lignes
                .GroupBy(l => l.ProduitId)
                .ToDictionary(g => g.Key, g => g.First());

            var bomsCache = await _db.Boms
                .Include(b => b.Lignes)
                    .ThenInclude(bl => bl.ComposantProduit)
                .ToListAsync();

            // IMPORTANT : on n ajoute en racine que les produits techniquement PF ou PF+SF
            foreach (var l in plan.Lignes)
            {
                if (!planProduitsDict.TryGetValue(l.ProduitId, out var prod))
                    continue;

                var typeMrp = MapTypeTechniqueToMrpType(prod.TypeTechnique);
                if (typeMrp != "PF" && typeMrp != "PF+SF")
                    continue; // SF/MP non visibles comme lignes racines

                var codePf = prod.Reference;
                var libPf = prod.Nom;
                var stockPfActuel = prod.QuantiteDisponible;

                var vmPf = new LigneMrpVm
                {
                    Niveau = 0,
                    CodeArticle = codePf,
                    ParentCodeArticle = null,
                    LibelleArticle = libPf,
                    TypeProduit = typeMrp,
                    Unite = "PCS",
                    DateBesoin = l.DateBesoin,
                    QuantiteBesoin = l.QuantiteBesoin,
                    StockDisponible = stockPfActuel,
                    QuantiteALancer = l.QuantiteALancer,
                    Prix = l.PrixTotal,
                    QuantiteParParent = 1m
                };

                Lignes.Add(vmPf);

                await AjouterComposantsRecursifsAsync(
                    produitParent: prod,
                    niveauParent: 0,
                    codeParent: codePf,
                    dateBesoin: l.DateBesoin,
                    bomsCache: bomsCache,
                    lignesPlanParProduitId: lignesPlanParProduitId);
            }

            await ConstruireInfosBomAsync();
            ConstruireStockMrp();
            ConstruireBomDetails();
            ConstruireBomRatios();
        }

        // surcharge avec dictionnaire des lignes de plan
        private async Task AjouterComposantsRecursifsAsync(
            Produit produitParent,
            int niveauParent,
            string codeParent,
            DateTime dateBesoin,
            List<Bom> bomsCache,
            Dictionary<int, MRPPlanLigne> lignesPlanParProduitId)
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
                var quantiteParParent = bl.Quantite;

                // Récupérer la ligne de plan correspondante pour cet article (si existe)
                lignesPlanParProduitId.TryGetValue(comp.Id, out var lignePlanComp);

                var quantiteALancerComp = lignePlanComp?.QuantiteALancer ?? 0m;
                var prixComp = lignePlanComp?.PrixTotal ?? comp.CoutBom;

                var vmComp = new LigneMrpVm
                {
                    Niveau = niveauParent + 1,
                    CodeArticle = codeComp,
                    ParentCodeArticle = codeParent,
                    LibelleArticle = libComp,
                    TypeProduit = typeComp,
                    Unite = "PCS",
                    DateBesoin = lignePlanComp?.DateBesoin ?? dateBesoin,
                    QuantiteBesoin = lignePlanComp?.QuantiteBesoin ?? 0m,
                    StockDisponible = lignePlanComp?.StockDisponible ?? comp.QuantiteDisponible,
                    QuantiteALancer = quantiteALancerComp,
                    Prix = prixComp,
                    QuantiteParParent = quantiteParParent
                };

                Lignes.Add(vmComp);

                if (typeComp == "SF" || typeComp == "PF+SF")
                {
                    await AjouterComposantsRecursifsAsync(
                        produitParent: comp,
                        niveauParent: niveauParent + 1,
                        codeParent: codeComp,
                        dateBesoin: dateBesoin,
                        bomsCache: bomsCache,
                        lignesPlanParProduitId: lignesPlanParProduitId);
                }
            }
        }

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
                    .Where(l => l.CodeArticle != codePf && l.ParentCodeArticle == codePf)
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
                    .Where(l => l.ParentCodeArticle == codePf)
                    .Select(l => new BomDetailComponentVm
                    {
                        CodeArticle = l.CodeArticle,
                        Nom = l.LibelleArticle,
                        QuantiteParParent = l.QuantiteParParent
                    })
                    .DistinctBy(c => c.CodeArticle)
                    .ToList();

                pf.Composants = composants;
                details.Add(pf);
            }

            BomDetailsJson = JsonSerializer.Serialize(details);
        }

        private void ConstruireBomRatios()
        {
            var ratios = new List<BomRatioVm>();

            foreach (var l in Lignes.Where(x => !string.IsNullOrEmpty(x.ParentCodeArticle)))
            {
                ratios.Add(new BomRatioVm
                {
                    ParentCodeArticle = l.ParentCodeArticle!,
                    EnfantCodeArticle = l.CodeArticle,
                    QuantiteParParent = l.QuantiteParParent
                });
            }

            BomRatiosJson = JsonSerializer.Serialize(ratios);
        }

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
                    Index = i,
                    Date = date,
                    LabelCourt = "P" + (i + 1),
                    LabelLong = date.ToString("dd/MM/yyyy")
                });
            }
        }

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

        // TELECHARGEMENT / VISUALISATION D'UN FICHIER OF
        public async Task<IActionResult> OnGetDownloadOFAsync(int id)
        {
            var fichier = await _db.MRPFichiers.FirstOrDefaultAsync(f => f.Id == id);
            if (fichier == null || fichier.FichierBlob == null || fichier.FichierBlob.Length == 0)
            {
                return NotFound();
            }

            var contentType = string.IsNullOrWhiteSpace(fichier.ContentType)
                ? "application/pdf"
                : fichier.ContentType;

            var fileName = string.IsNullOrWhiteSpace(fichier.FichierNom)
                ? fichier.ReferenceOF + ".pdf"
                : fichier.FichierNom;

            return File(fichier.FichierBlob, contentType, fileName);
        }

        // charge les fichiers d'OF pour une planification donnée
        private async Task ChargerFichiersOFAsync(int planId)
        {
            FichiersOF = await _db.MRPFichiers
                .Where(f => f.PlanificationId == planId)
                .OrderByDescending(f => f.DateOrdre)
                .ToListAsync();
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
