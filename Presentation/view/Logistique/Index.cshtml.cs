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
            await _logistiqueService.CleanupAbandonedTrajetsAsync(15);
            Vehicules = await _logistiqueService.GetVehiculesAsync();
        }

        public async Task<IActionResult> OnPostAddVehiculeAsync(
            string nom, string matricule, string type,
            string? marque, string? modele, int? annee, string? carburant)
        {
            if (!string.IsNullOrEmpty(nom))
            {
                // Estimation CO2 formule immédiate
                double co2 = LogistiqueService.EstimerCO2ParKmFormule(carburant, type, annee);

                var v = new Vehicule
                {
                    Nom = nom,
                    Matricule = matricule,
                    TypeTransport = type,
                    Statut = "Disponible",
                    Marque = marque,
                    Modele = modele,
                    Annee = annee,
                    TypeCarburant = carburant,
                    EmissionCO2ParKm = co2
                };
                await _logistiqueService.CreateVehiculeAsync(v);

                // Affiner avec Gemini en arrière-plan (non bloquant)
                if (!string.IsNullOrWhiteSpace(marque) && !string.IsNullOrWhiteSpace(modele))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            double co2Affine = await _logistiqueService.EstimerCO2ParKmAvecIAAsync(
                                marque, modele, annee, carburant, type);
                            v.EmissionCO2ParKm = co2Affine;
                            await _logistiqueService.CreateVehiculeAsync(v); // Update via save context
                        }
                        catch { /* non bloquant */ }
                    });
                }
            }
            return RedirectToPage();
        }

    }
}
