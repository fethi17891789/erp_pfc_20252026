// Fichier : Presentation/view/Achats/BonReception.cshtml.cs
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Donnees;
using Donnees.Achats;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages.Achats
{
    public class BonReceptionModel : PageModel
    {
        private readonly AchatsService _achatsService;
        private readonly ErpDbContext   _db;

        public BonReceptionModel(AchatsService achatsService, ErpDbContext db)
        {
            _achatsService = achatsService;
            _db            = db;
        }

        // ─── Données d'affichage ───────────────────────────────────────────────
        public AchatBonReception?  BonReception { get; set; }
        public AchatBonCommande?   BonCommande  { get; set; }

        /// <summary>true = formulaire de création, false = vue détail d'un BR existant</summary>
        public bool ModeCreation => BonReception == null;

        public string? MessageSucces { get; set; }
        public string? MessageErreur { get; set; }

        // ─── Champs du formulaire ──────────────────────────────────────────────
        [BindProperty] public string  DateReceptionStr { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
        [BindProperty] public string? Notes           { get; set; }
        [BindProperty] public string  LignesJson      { get; set; } = "[]";

        // ─── GET ───────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(int? id, int? bcId)
        {
            var config = await _achatsService.GetConfigAsync();
            if (config?.EstConfigure != true)
                return RedirectToPage("/Achats/Config");

            if (id.HasValue)
            {
                // Mode détail : affichage d'un BR existant
                BonReception = await _achatsService.GetBonReceptionAsync(id.Value);
                if (BonReception == null) return NotFound();
                BonCommande = BonReception.BonCommande;
            }
            else if (bcId.HasValue)
            {
                // Mode création : pré-remplissage depuis le BC
                BonCommande = await _achatsService.GetBonCommandeAsync(bcId.Value);
                if (BonCommande == null) return NotFound();

                // Seuls les BC confirmés ou partiellement reçus peuvent donner lieu à un BR
                if (BonCommande.Statut != StatutBonCommande.Confirme
                    && BonCommande.Statut != StatutBonCommande.PartiellemtRecu)
                    return RedirectToPage("/Achats/BonCommande", new { id = bcId });
            }
            else
            {
                return RedirectToPage("/Achats/Hub");
            }

            if (TempData["Succes"] is string msg) MessageSucces = msg;
            if (TempData["Erreur"] is string err) MessageErreur = err;
            return Page();
        }

        // ─── POST : Créer et valider un BR ────────────────────────────────────
        public async Task<IActionResult> OnPostCreerAsync(int bcId)
        {
            int? userId = HttpContext.Session.GetInt32("CurrentUserId");

            // Désérialiser les lignes JSON soumises par le formulaire
            var lignesInput = new List<(int ProduitId, decimal QteCmd, decimal QteRecue, string Etat)>();
            try
            {
                var raw = JsonSerializer.Deserialize<List<LigneBrJson>>(LignesJson ?? "[]") ?? new();
                foreach (var l in raw)
                    lignesInput.Add((l.produitId, l.quantiteCommandee, l.quantiteRecue, l.etat));
            }
            catch { /* lignes vides */ }

            if (lignesInput.Count == 0)
            {
                TempData["Erreur"] = "Le bon de réception doit contenir au moins une ligne.";
                return RedirectToPage(new { bcId });
            }

            DateTime dateReception = DateTime.Today;
            if (!string.IsNullOrEmpty(DateReceptionStr)
                && DateTime.TryParse(DateReceptionStr, out var dr))
                dateReception = dr;

            // Créer le BR
            var br = await _achatsService.CreerBonReceptionAsync(
                bcId, dateReception, Notes?.Trim(), lignesInput, userId);

            // Valider immédiatement (mise à jour du stock + historique prix)
            await _achatsService.ValiderBonReceptionAsync(br.Id, userId);

            TempData["Succes"] = $"Bon de réception {br.Numero} créé et stock mis à jour.";
            return RedirectToPage(new { id = br.Id });
        }

        // ─── POST : Re-valider un BR EnCours (cas rare) ───────────────────────
        public async Task<IActionResult> OnPostValiderAsync(int id)
        {
            int? userId = HttpContext.Session.GetInt32("CurrentUserId");
            await _achatsService.ValiderBonReceptionAsync(id, userId);
            TempData["Succes"] = "Bon de réception validé — stock mis à jour.";
            return RedirectToPage(new { id });
        }

        // ─── Helpers vue ──────────────────────────────────────────────────────
        public static (string bg, string color, string label) GetEtatStyle(string etat) => etat switch
        {
            EtatReceptionLigne.Conforme  => ("rgba(34,197,94,0.10)",   "#22c55e", "Conforme"),
            EtatReceptionLigne.Endommage => ("rgba(251,191,36,0.10)",  "#fbbf24", "Endommagé"),
            EtatReceptionLigne.Manquant  => ("rgba(239,68,68,0.10)",   "#ef4444", "Manquant"),
            _                            => ("rgba(255,255,255,0.06)", "#a4a7c8", etat)
        };

        // ─── DTO interne ──────────────────────────────────────────────────────
        private class LigneBrJson
        {
            public int     produitId         { get; set; }
            public decimal quantiteCommandee { get; set; }
            public decimal quantiteRecue     { get; set; }
            public string  etat              { get; set; } = EtatReceptionLigne.Conforme;
        }
    }
}
