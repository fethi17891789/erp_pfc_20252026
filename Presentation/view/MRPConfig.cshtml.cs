// Fichier : Pages/MRPConfig.cshtml.cs
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Metier.MRP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages
{
    public class MRPConfigModel : PageModel
    {
        private readonly MRPConfigService _configService;

        public MRPConfigModel(MRPConfigService configService)
        {
            _configService = configService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Display(Name = "Horizon par d�faut (en jours)")]
            [Range(1, 365, ErrorMessage = "L'horizon doit �tre entre 1 et 365 jours.")]
            public int HorizonParDefautJours { get; set; }
        }

        public async Task OnGetAsync()
        {
            var cfg = await _configService.GetConfigAsync();

            if (cfg == null)
            {
                // Pas encore de config en base : on affiche juste des valeurs par d�faut c�t� UI
                Input = new InputModel
                {
                    HorizonParDefautJours = 30
                };
            }
            else
            {
                Input = new InputModel
                {
                    HorizonParDefautJours = cfg.HorizonParDefautJours
                };
            }
        }

        public async Task<IActionResult> OnPostSaveConfigAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            int? userId = null; // � remplir si tu as un syst�me d'utilisateur

            // Ici, si la config n'existe pas, UpdateHorizonAsync la cr�era
            await _configService.UpdateHorizonAsync(Input.HorizonParDefautJours, userId);

            TempData["MRPConfigSaved"] = "Configuration MRP enregistr�e avec succ�s.";

            // Apr�s enregistrement, on revient sur la page du module MRP
            return RedirectToPage("/MRP");
        }

    }
}
