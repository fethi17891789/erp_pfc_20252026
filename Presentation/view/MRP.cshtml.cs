// Fichier : Pages/MRP.cshtml.cs
using System.Threading.Tasks;
using Metier.MRP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages
{
    public class MRPModel : PageModel
    {
        private readonly MRPConfigService _configService;

        public MRPModel(MRPConfigService configService)
        {
            _configService = configService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Vérifier si la config existe déjà SANS la créer
            var cfg = await _configService.GetConfigAsync();

            if (cfg == null)
            {
                // Pas encore configuré -> on envoie vers la page de configuration
                return RedirectToPage("/MRPConfig");
            }

            // Plus tard tu pourras charger des données MRP ici
            return Page();
        }

        public IActionResult OnPostLogout()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/BDDView");
        }
    }
}
