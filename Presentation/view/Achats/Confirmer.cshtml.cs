// Fichier : Presentation/view/Achats/Confirmer.cshtml.cs
using System;
using System.Threading.Tasks;
using Donnees.Achats;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages.Achats
{
    /// <summary>
    /// Page publique (sans login) accessible via un lien email envoyé au fournisseur.
    /// Permet au fournisseur de confirmer ou refuser un bon de commande,
    /// et éventuellement de proposer une autre date de livraison.
    /// </summary>
    public class ConfirmerModel : PageModel
    {
        private readonly AchatsService _achatsService;

        public ConfirmerModel(AchatsService achatsService)
        {
            _achatsService = achatsService;
        }

        // ─── Données d'affichage ───────────────────────────────────────────────
        public AchatBonCommande? BonCommande { get; set; }
        public bool TokenValide { get; set; }
        public bool ReponseEnregistree { get; set; }
        public string? MessageResultat { get; set; }
        public bool EstConfirme { get; set; }

        // ─── Formulaire (token en query string) ───────────────────────────────
        [BindProperty(SupportsGet = true)]
        public string? Token { get; set; }

        [BindProperty] public string? Reponse { get; set; }               // "confirmer" | "refuser"
        [BindProperty] public string? MessageFournisseur { get; set; }
        [BindProperty] public string? DateLivraisonProposeeStr { get; set; }

        // ─── GET ───────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(Token))
            {
                TokenValide = false;
                return Page();
            }

            BonCommande = await _achatsService.GetBonCommandeParTokenAsync(Token);
            TokenValide = BonCommande != null;
            return Page();
        }

        // ─── POST ──────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Token))
            {
                TokenValide = false;
                ReponseEnregistree = true;
                MessageResultat = "Lien invalide ou expiré.";
                return Page();
            }

            bool confirme = Reponse == "confirmer";

            DateTime? dateLivraison = null;
            if (!string.IsNullOrEmpty(DateLivraisonProposeeStr)
                && DateTime.TryParse(DateLivraisonProposeeStr, out var dl))
                dateLivraison = dl;

            bool ok = await _achatsService.TraiterReponsFournisseurAsync(
                Token, confirme, MessageFournisseur?.Trim(), dateLivraison);

            ReponseEnregistree = true;
            EstConfirme = confirme;

            if (!ok)
            {
                TokenValide = false;
                MessageResultat = "Ce lien est invalide ou a déjà été utilisé.";
            }
            else
            {
                TokenValide = true;
                MessageResultat = confirme
                    ? "Commande confirmée. L'acheteur a été notifié."
                    : "Commande refusée. L'acheteur a été notifié.";
            }

            return Page();
        }
    }
}
