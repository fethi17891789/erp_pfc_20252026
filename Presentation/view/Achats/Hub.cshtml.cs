// Fichier : Presentation/view/Achats/Hub.cshtml.cs
using System.Threading.Tasks;
using Donnees;
using Donnees.Achats;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages.Achats
{
    public class HubModel : PageModel
    {
        private readonly AchatsService _achatsService;
        private readonly ErpDbContext  _db;

        public HubModel(AchatsService achatsService, ErpDbContext db)
        {
            _achatsService = achatsService;
            _db            = db;
        }

        // ─── KPIs affichés sur les cartes ─────────────────────────────────────
        public int NbBCTotal      { get; set; }
        public int NbBCEnAttente  { get; set; }
        public int NbBRTotal      { get; set; }
        public int NbFactures     { get; set; }
        public bool EstConfigure  { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var config = await _achatsService.GetConfigAsync();
            EstConfigure = config?.EstConfigure == true;

            if (EstConfigure)
            {
                NbBCTotal     = await _db.AchatBonCommandes.CountAsync();
                NbBCEnAttente = await _achatsService.CompterBCEnCoursAsync();
                NbBRTotal     = await _db.AchatBonReceptions.CountAsync();
                NbFactures    = await _db.AchatFacturesFournisseur.CountAsync();
            }

            return Page();
        }
    }
}
