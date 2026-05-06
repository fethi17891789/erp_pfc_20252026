// Fichier : Presentation/view/Achats/Confirmer.cshtml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Donnees.Achats;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages.Achats
{
    public class ConfirmerModel : PageModel
    {
        private readonly AchatsService _achatsService;

        public ConfirmerModel(AchatsService achatsService)
        {
            _achatsService = achatsService;
        }

        public AchatNegociationTentative? Tentative { get; set; }
        public bool DejaRepondu          { get; set; }
        public bool Succes               { get; set; }
        public bool EstContreProposition { get; set; }
        public bool EstRefusTotal        { get; set; }

        // ── GET ───────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return Page();

            Tentative = await _achatsService.GetTentativeParTokenAsync(token);
            if (Tentative == null)
                DejaRepondu = true;

            return Page();
        }

        // ── POST ──────────────────────────────────────────────────────────
        [BindProperty] public string Token              { get; set; } = string.Empty;
        [BindProperty] public string? MessageFournisseur { get; set; }
        [BindProperty] public string Action             { get; set; } = "accepter";
        [BindProperty] public List<int>      LigneIds       { get; set; } = new();
        [BindProperty] public List<string?>  PrixProposes   { get; set; } = new();
        [BindProperty] public List<bool>     LignesRefusees { get; set; } = new();

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Token))
                return Page();

            Tentative = await _achatsService.GetTentativeParTokenAsync(Token);
            if (Tentative == null) { DejaRepondu = true; return Page(); }

            // Refus total : BC annulé instantanément
            if (Action == "refuser")
            {
                bool ok = await _achatsService.RefuserDefinitivementAsync(Token, MessageFournisseur);
                Succes = ok;
                EstContreProposition = false;
                EstRefusTotal = true;
                if (!ok) DejaRepondu = true;
                return Page();
            }

            var reponsesLignes = new List<(int LigneId, decimal? PrixProposeHT, bool EstRefusee)>();

            for (int i = 0; i < LigneIds.Count; i++)
            {
                bool refusee  = i < LignesRefusees.Count && LignesRefusees[i];
                decimal? prix = null;

                if (!refusee && i < PrixProposes.Count)
                {
                    string? raw = PrixProposes[i];
                    if (!string.IsNullOrWhiteSpace(raw) && decimal.TryParse(
                            raw,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out decimal p))
                    {
                        var ligneBC = Tentative.BonCommande?.Lignes.FirstOrDefault(l => l.Id == LigneIds[i]);
                        prix = (ligneBC != null && p == ligneBC.PrixUnitaireHT) ? null : p;
                    }
                }

                reponsesLignes.Add((LigneIds[i], prix, refusee));
            }

            if (Action == "accepter")
                reponsesLignes = reponsesLignes.Select(r => (r.LigneId, (decimal?)null, false)).ToList();

            bool contrePropo = reponsesLignes.Any(r => r.EstRefusee || r.PrixProposeHT.HasValue);
            EstContreProposition = contrePropo;

            bool resultat = await _achatsService.TraiterReponseNegociationAsync(
                Token, MessageFournisseur, reponsesLignes);

            Succes = resultat;
            if (!resultat) DejaRepondu = true;

            return Page();
        }
    }
}
