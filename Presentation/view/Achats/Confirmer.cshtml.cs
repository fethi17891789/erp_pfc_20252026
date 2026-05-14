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

        public async Task<IActionResult> OnGetAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return Page();

            Tentative = await _achatsService.GetTentativeParTokenAsync(token);
            if (Tentative == null)
                DejaRepondu = true;

            return Page();
        }

        [BindProperty] public string Token              { get; set; } = string.Empty;
        [BindProperty] public string? MessageFournisseur { get; set; }
        [BindProperty] public string Action             { get; set; } = "proforma";
        [BindProperty] public List<int>      LigneIds         { get; set; } = new();
        [BindProperty] public List<string?>  PrixProposes     { get; set; } = new();
        [BindProperty] public List<string?>  QtesProposees    { get; set; } = new();

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Token))
                return Page();

            Tentative = await _achatsService.GetTentativeParTokenAsync(Token);
            if (Tentative == null) { DejaRepondu = true; return Page(); }

            if (Action == "refuser")
            {
                bool ok = await _achatsService.RefuserDefinitivementAsync(Token, MessageFournisseur);
                Succes = ok;
                EstContreProposition = false;
                EstRefusTotal = true;
                if (!ok) DejaRepondu = true;
                return Page();
            }

            var reponsesLignes = new List<(int LigneId, decimal? PrixProposeHT, decimal? QuantiteProposee, bool EstRefusee)>();

            for (int i = 0; i < LigneIds.Count; i++)
            {
                decimal? prix = null;
                decimal? qte  = null;

                if (i < PrixProposes.Count && !string.IsNullOrWhiteSpace(PrixProposes[i]))
                {
                    if (decimal.TryParse(PrixProposes[i],
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out decimal p))
                        prix = p;
                }

                if (i < QtesProposees.Count && !string.IsNullOrWhiteSpace(QtesProposees[i]))
                {
                    if (decimal.TryParse(QtesProposees[i],
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out decimal q))
                    {
                        var ligneBC = Tentative.BonCommande?.Lignes.FirstOrDefault(l => l.Id == LigneIds[i]);
                        qte = (ligneBC != null && q == ligneBC.Quantite) ? null : q;
                    }
                }

                reponsesLignes.Add((LigneIds[i], prix, qte, false));
            }

            EstContreProposition = true;

            bool resultat = await _achatsService.TraiterReponseNegociationAsync(
                Token, MessageFournisseur, reponsesLignes);

            Succes = resultat;
            if (!resultat) DejaRepondu = true;

            return Page();
        }
    }
}
