// Fichier : Presentation/view/Achats/Config.cshtml.cs
using System.Threading.Tasks;
using Donnees.Achats;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages.Achats
{
    public class ConfigModel : PageModel
    {
        private readonly AchatsService _achatsService;

        public ConfigModel(AchatsService achatsService)
        {
            _achatsService = achatsService;
        }

        [BindProperty]
        public PolitiquePrixAchat PolitiqueChoisie { get; set; } = PolitiquePrixAchat.DernierPrix;

        /// <summary>
        /// Vrai si on arrive depuis les Paramètres (reconfiguration), faux si c'est le premier lancement.
        /// </summary>
        public bool EstReconfiguration { get; set; } = false;

        public async Task<IActionResult> OnGetAsync()
        {
            var config = await _achatsService.GetConfigAsync();

            // Si déjà configuré → mode reconfiguration (on affiche quand même la page)
            EstReconfiguration = config?.EstConfigure == true;

            // Pré-sélectionner la politique actuelle si elle existe
            if (config != null)
                PolitiqueChoisie = config.PolitiquePrix;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            int? userId = HttpContext.Session.GetInt32("CurrentUserId");
            await _achatsService.ConfigurerModuleAsync(PolitiqueChoisie, userId);
            return RedirectToPage("/Achats/Index");
        }
    }
}
