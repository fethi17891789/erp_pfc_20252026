using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Metier.Logistique;
using Donnees.Logistique;
using Donnees;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages.Logistique
{
    public class IndexModel : PageModel
    {
        private readonly LogistiqueService _logistiqueService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ErpDbContext _context;

        public IndexModel(LogistiqueService logistiqueService, IServiceScopeFactory scopeFactory, ErpDbContext context)
        {
            _logistiqueService = logistiqueService;
            _scopeFactory = scopeFactory;
            _context = context;
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
                // Utilise IServiceScopeFactory pour éviter d'utiliser un DbContext déjà disposé
                if (!string.IsNullOrWhiteSpace(marque) && !string.IsNullOrWhiteSpace(modele))
                {
                    int vehiculeId = v.Id;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var svc = scope.ServiceProvider.GetRequiredService<LogistiqueService>();
                            double co2Affine = await svc.EstimerCO2ParKmAvecIAAsync(
                                marque, modele, annee, carburant, type);
                            await svc.UpdateCO2VehiculeAsync(vehiculeId, co2Affine);
                        }
                        catch { /* non bloquant */ }
                    });
                }
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetClientsGeoAsync()
        {
            var clients = await _context.Contacts
                .Where(c => (c.Roles & ContactRole.Client) != 0
                         && c.Latitude != null && c.Longitude != null)
                .Select(c => new {
                    id = c.Id,
                    nom = c.FullName,
                    adresse = c.AdresseComplete,
                    lat = c.Latitude,
                    lon = c.Longitude
                })
                .ToListAsync();
            return new JsonResult(clients);
        }

        public async Task<IActionResult> OnPostUpdateVehiculeAsync(
            int id, string nom, string matricule, string type,
            string? marque, string? modele, int? annee, string? carburant)
        {
            if (id > 0 && !string.IsNullOrEmpty(nom))
            {
                var updated = await _logistiqueService.UpdateVehiculeAsync(
                    id, nom, matricule, type, marque, modele, annee, carburant);

                // Affiner CO2 avec Gemini en arrière-plan (non bloquant)
                if (updated != null && !string.IsNullOrWhiteSpace(marque) && !string.IsNullOrWhiteSpace(modele))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var svc = scope.ServiceProvider.GetRequiredService<LogistiqueService>();
                            double co2Affine = await svc.EstimerCO2ParKmAvecIAAsync(
                                marque, modele, annee, carburant, type);
                            await svc.UpdateCO2VehiculeAsync(id, co2Affine);
                        }
                        catch { /* non bloquant */ }
                    });
                }
            }
            return RedirectToPage();
        }


    }
}
