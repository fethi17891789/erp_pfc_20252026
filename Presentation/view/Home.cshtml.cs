// Fichier : Presentation/view/Home.cshtml.cs
using System.Threading.Tasks;
using Donnees;
using Donnees.Achats;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages
{
    public class HomeModel : PageModel
    {
        private readonly ErpDbContext  _context;
        private readonly AchatsService _achatsService;

        public HomeModel(ErpDbContext context, AchatsService achatsService)
        {
            _context       = context;
            _achatsService = achatsService;
        }

        // ─── KPIs généraux ────────────────────────────────────────────────────
        public int NbProduits { get; set; }
        public int NbContacts { get; set; }
        public int NbVehiculesEnRoute { get; set; }
        public int NbVehiculesTotal { get; set; }

        // ─── KPIs Achats ──────────────────────────────────────────────────────
        public int  NbBCEnAttente  { get; set; }
        public int  NbBCConfirmes  { get; set; }
        public int  NbBCRefuses    { get; set; }
        public int  NbBCFactures   { get; set; }
        public bool AchatsActive   { get; set; }

        public async Task OnGetAsync()
        {
            NbProduits = await _context.Produits.CountAsync();
            NbContacts = await _context.Contacts.CountAsync();
            NbVehiculesTotal = await _context.LogistiqueVehicules.CountAsync();
            NbVehiculesEnRoute = await _context.LogistiqueVehicules
                .CountAsync(v => v.Statut == "En Trajet");

            // KPIs Achats — uniquement si le module est configuré
            var config = await _achatsService.GetConfigAsync();
            AchatsActive = config?.EstConfigure == true;
            if (AchatsActive)
            {
                NbBCEnAttente = await _achatsService.CompterBCEnCoursAsync();
                NbBCRefuses   = await _achatsService.CompterBCRefusesAsync();
                NbBCConfirmes = await _context.AchatBonCommandes
                    .CountAsync(b => b.Statut == StatutBonCommande.Confirme);
                NbBCFactures  = await _context.AchatBonCommandes
                    .CountAsync(b => b.Statut == StatutBonCommande.Facture);
            }
        }
    }
}
