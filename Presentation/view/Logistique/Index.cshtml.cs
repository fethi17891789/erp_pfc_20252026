using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Metier.Logistique;
using Donnees.Logistique;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace erp_pfc_20252026.Pages.Logistique
{
    public class IndexModel : PageModel
    {
        private readonly LogistiqueService _logistiqueService;

        public IndexModel(LogistiqueService logistiqueService)
        {
            _logistiqueService = logistiqueService;
        }

        public List<Vehicule> Vehicules { get; set; } = new List<Vehicule>();

        public async Task OnGetAsync()
        {
            Vehicules = await _logistiqueService.GetVehiculesAsync();
        }

        public async Task<IActionResult> OnPostAddVehiculeAsync(string nom, string matricule, string type)
        {
            if (!string.IsNullOrEmpty(nom))
            {
                var v = new Vehicule
                {
                    Nom = nom,
                    Matricule = matricule,
                    TypeTransport = type,
                    Statut = "Disponible"
                };
                await _logistiqueService.CreateVehiculeAsync(v);
            }
            return RedirectToPage();
        }
    }
}
