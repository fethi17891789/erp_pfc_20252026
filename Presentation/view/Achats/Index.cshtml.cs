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
        public string? MessageSucces { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var config = await _achatsService.GetConfigAsync();
            if (config?.EstConfigure != true)
                return RedirectToPage("/Achats/Config");

            BonCommandes = await _achatsService.GetBonCommandesAsync();

            NbBCEnCours   = await _achatsService.CompterBCEnCoursAsync();
            NbBCRefuses   = await _achatsService.CompterBCRefusesAsync();
            NbBCConfirmes = await _db.AchatBonCommandes
                .CountAsync(b => b.Statut == StatutBonCommande.Confirme);
            NbBCFactures  = await _db.AchatBonCommandes
                .CountAsync(b => b.Statut == StatutBonCommande.Facture);

            if (TempData["Succes"] is string msg) MessageSucces = msg;

            return Page();
        }
    }
}
