// Fichier : Presentation/view/Achats/Index.cshtml.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Donnees.Achats;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Donnees;

namespace erp_pfc_20252026.Pages.Achats
{
    public class IndexModel : PageModel
    {
        private readonly AchatsService _achatsService;
        private readonly ErpDbContext _db;

        public IndexModel(AchatsService achatsService, ErpDbContext db)
        {
            _achatsService = achatsService;
            _db = db;
        }

        public List<AchatBonCommande> BonCommandes { get; set; } = new();
        public int NbBCEnCours    { get; set; }
        public int NbBCRefuses    { get; set; }
        public int NbBCConfirmes  { get; set; }
        public int NbBCFactures   { get; set; }

        // Totaux pour le dropdown filtre (indépendants du filtre courant)
        public int NbTotalCommandes  { get; set; }
        public int NbTotalReceptions { get; set; }
        public int NbTotalFactures   { get; set; }

        public string? MessageSucces { get; set; }

        // Filtre optionnel transmis depuis le Hub : "receptions" ou "factures"
        [BindProperty(SupportsGet = true)]
        public string? Filtre { get; set; }

        public string TitreFiltre     { get; set; } = "Bons de commande";
        public string SousTitreFiltre { get; set; } = "Gérez vos commandes fournisseurs de A à Z";

        public async Task<IActionResult> OnGetAsync()
        {
            var config = await _achatsService.GetConfigAsync();
            if (config?.EstConfigure != true)
                return RedirectToPage("/Achats/Config");

            var query = _db.AchatBonCommandes
                .Include(b => b.Fournisseur)
                .Include(b => b.Lignes).ThenInclude(l => l.Produit)
                .AsQueryable();

            if (Filtre == "receptions")
            {
                query = query.Where(b =>
                    b.Statut == StatutBonCommande.Confirme ||
                    b.Statut == StatutBonCommande.PartiellemtRecu ||
                    b.Statut == StatutBonCommande.Recu);
                TitreFiltre     = "Bons de réception";
                SousTitreFiltre = "BCs confirmés ou en cours de réception";
            }
            else if (Filtre == "factures")
            {
                query = query.Where(b =>
                    b.Statut == StatutBonCommande.Recu ||
                    b.Statut == StatutBonCommande.Facture);
                TitreFiltre     = "Factures fournisseurs";
                SousTitreFiltre = "BCs reçus — prêts à être facturés ou déjà facturés";
            }

            BonCommandes = await query.OrderByDescending(b => b.DateCreation).ToListAsync();

            NbBCEnCours   = await _achatsService.CompterBCEnCoursAsync();
            NbBCRefuses   = await _achatsService.CompterBCRefusesAsync();
            NbBCConfirmes = await _db.AchatBonCommandes
                .CountAsync(b => b.Statut == StatutBonCommande.Confirme);
            NbBCFactures  = await _db.AchatBonCommandes
                .CountAsync(b => b.Statut == StatutBonCommande.Facture);

            // Totaux pour le dropdown filtre (basés sur l'ensemble de la table, pas sur le filtre courant)
            NbTotalCommandes  = await _db.AchatBonCommandes.CountAsync();
            NbTotalReceptions = await _db.AchatBonCommandes.CountAsync(b =>
                b.Statut == StatutBonCommande.Confirme ||
                b.Statut == StatutBonCommande.PartiellemtRecu ||
                b.Statut == StatutBonCommande.Recu);
            NbTotalFactures   = await _db.AchatBonCommandes.CountAsync(b =>
                b.Statut == StatutBonCommande.Recu ||
                b.Statut == StatutBonCommande.Facture);

            if (TempData["Succes"] is string msg) MessageSucces = msg;

            return Page();
        }
    }
}
