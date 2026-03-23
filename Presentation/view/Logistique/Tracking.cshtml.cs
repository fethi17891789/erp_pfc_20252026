using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Metier.Logistique;
using Donnees.Logistique;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace erp_pfc_20252026.Pages.Logistique
{
    public class TrackingModel : PageModel
    {
        private readonly LogistiqueService _logistiqueService;

        public TrackingModel(LogistiqueService logistiqueService)
        {
            _logistiqueService = logistiqueService;
        }

        public List<Vehicule> VehiculesDisponibles { get; set; } = new List<Vehicule>();
        
        [BindProperty(SupportsGet = true)]
        public string DeviceId { get; set; }

        public async Task OnGetAsync()
        {
            // Charger les véhicules disponibles pour le tracking
            VehiculesDisponibles = await _logistiqueService.GetVehiculesAsync();
        }
    }
}
