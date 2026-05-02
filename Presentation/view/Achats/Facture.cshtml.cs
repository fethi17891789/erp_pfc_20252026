// Fichier : Presentation/view/Achats/Facture.cshtml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Donnees;
using Donnees.Achats;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages.Achats
{
    public class FactureModel : PageModel
    {
        private readonly AchatsService _achatsService;
        private readonly ErpDbContext   _db;

        public FactureModel(AchatsService achatsService, ErpDbContext db)
        {
            _achatsService = achatsService;
            _db            = db;
        }

        // ─── Données d'affichage ───────────────────────────────────────────────
        public AchatFactureFournisseur? Facture     { get; set; }
        public AchatBonCommande?        BonCommande { get; set; }

        /// <summary>Bons de réception validés pour ce BC — pour choisir le BR associé</summary>
        public List<AchatBonReception> BonsReception { get; set; } = new();

        public bool ModeCreation => Facture == null;
        public string? MessageSucces { get; set; }

        // ─── Champs du formulaire ──────────────────────────────────────────────
        [BindProperty] public string? NumeroFournisseur { get; set; }
        [BindProperty] public string  DateFactureStr    { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
        [BindProperty] public string  MontantHTStr      { get; set; } = "0";
        [BindProperty] public int?    BonReceptionId    { get; set; }
        [BindProperty] public string? Notes             { get; set; }

        // ─── GET ───────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(int? id, int? bcId)
        {
            var config = await _achatsService.GetConfigAsync();
            if (config?.EstConfigure != true)
                return RedirectToPage("/Achats/Config");

            if (id.HasValue)
            {
                // Mode détail
                Facture = await _achatsService.GetFactureAsync(id.Value);
                if (Facture == null) return NotFound();
                BonCommande = Facture.BonCommande;
            }
            else if (bcId.HasValue)
            {
                // Mode création
                BonCommande = await _achatsService.GetBonCommandeAsync(bcId.Value);
                if (BonCommande == null) return NotFound();

                // Pré-remplir le montant HT depuis le BC
                MontantHTStr = BonCommande.TotalHT.ToString("0.##");

                // Charger les BR validés pour ce BC
                BonsReception = await _db.AchatBonReceptions
                    .Where(r => r.BonCommandeId == bcId && r.Statut == StatutBonReception.Valide)
                    .OrderByDescending(r => r.DateCreation)
                    .ToListAsync();
            }
            else
            {
                return RedirectToPage("/Achats/Hub");
            }

            if (TempData["Succes"] is string msg) MessageSucces = msg;
            return Page();
        }

        // ─── POST : Créer la facture ───────────────────────────────────────────
        public async Task<IActionResult> OnPostCreerAsync(int bcId)
        {
            int? userId = HttpContext.Session.GetInt32("CurrentUserId");

            if (!decimal.TryParse(MontantHTStr?.Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal montantHT) || montantHT <= 0)
            {
                TempData["Erreur"] = "Montant HT invalide.";
                return RedirectToPage(new { bcId });
            }

            DateTime dateFacture = DateTime.Today;
            if (!string.IsNullOrEmpty(DateFactureStr)
                && DateTime.TryParse(DateFactureStr, out var df))
                dateFacture = df;

            var facture = await _achatsService.CreerFactureAsync(
                bcId, BonReceptionId, NumeroFournisseur?.Trim(),
                dateFacture, montantHT, Notes?.Trim(), userId);

            TempData["Succes"] = $"Facture {facture.Numero} enregistrée.";
            if (facture.AlerteEcartPrix)
                TempData["Succes"] += $" ⚠ Écart de {facture.EcartPourcentage:N1}% détecté par rapport au BC.";

            return RedirectToPage(new { id = facture.Id });
        }

        // ─── Helpers vue ──────────────────────────────────────────────────────
        public static (string bg, string color, string label) GetStatutStyle(string statut) => statut switch
        {
            StatutFactureFournisseur.Recue         => ("rgba(56,189,248,0.10)",  "#38bdf8", "Reçue"),
            StatutFactureFournisseur.Verifiee      => ("rgba(34,197,94,0.10)",   "#22c55e", "Vérifiée"),
            StatutFactureFournisseur.Comptabilisee => ("rgba(192,132,252,0.10)", "#c084fc", "Comptabilisée"),
            _                                      => ("rgba(255,255,255,0.06)", "#a4a7c8", statut)
        };
    }
}
