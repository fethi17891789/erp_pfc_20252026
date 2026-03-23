using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Donnees;
using Donnees.Logistique;
using Microsoft.EntityFrameworkCore;

namespace Metier.Logistique
{
    public class LogistiqueService
    {
        private readonly ErpDbContext _context;

        public LogistiqueService(ErpDbContext context)
        {
            _context = context;
        }

        #region Véhicules
        public async Task<List<Vehicule>> GetVehiculesAsync()
        {
            return await _context.LogistiqueVehicules.OrderBy(v => v.Nom).ToListAsync();
        }

        public async Task<Vehicule> GetVehiculeByIdAsync(int id)
        {
            return await _context.LogistiqueVehicules.FindAsync(id);
        }

        public async Task<Vehicule> CreateVehiculeAsync(Vehicule v)
        {
            _context.LogistiqueVehicules.Add(v);
            await _context.SaveChangesAsync();
            return v;
        }

        public async Task UpdatePositionVehiculeAsync(int vehiculeId, double lat, double lon)
        {
            var v = await _context.LogistiqueVehicules.FindAsync(vehiculeId);
            if (v != null)
            {
                v.Latitude = lat;
                v.Longitude = lon;
                v.DerniereMiseAJour = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        #endregion

        #region Capteurs
        public async Task<List<Capteur>> GetCapteursAsync()
        {
            return await _context.LogistiqueCapteurs.ToListAsync();
        }

        public async Task<Capteur> GetOrCreateCapteurByUidAsync(string uid)
        {
            var capteur = await _context.LogistiqueCapteurs
                .FirstOrDefaultAsync(c => c.IdentifiantUnique == uid);

            if (capteur == null)
            {
                capteur = new Capteur { IdentifiantUnique = uid, Description = "Nouveau téléphone" };
                _context.LogistiqueCapteurs.Add(capteur);
                await _context.SaveChangesAsync();
            }

            return capteur;
        }
        #endregion

        #region Trajets
        public async Task<Trajet> StartTrajetAsync(int vehiculeId, int capteurId, string origine = "")
        {
            var trajet = new Trajet
            {
                VehiculeId = vehiculeId,
                CapteurId = capteurId,
                DateDebut = DateTime.UtcNow,
                Origine = origine,
                Statut = "En Cours"
            };

            _context.LogistiqueTrajets.Add(trajet);
            
            // Mettre à jour le statut du véhicule
            var v = await _context.LogistiqueVehicules.FindAsync(vehiculeId);
            if (v != null) v.Statut = "En Trajet";

            await _context.SaveChangesAsync();
            return trajet;
        }

        public async Task EndTrajetAsync(int trajetId, string destination = "", double distance = 0, string traceJson = null)
        {
            var trajet = await _context.LogistiqueTrajets.FindAsync(trajetId);
            if (trajet != null)
            {
                trajet.DateFin = DateTime.UtcNow;
                trajet.Destination = destination;
                trajet.DistanceParcourueKm = distance;
                trajet.TraceJson = traceJson;
                trajet.Statut = "Termine";

                // Libérer le véhicule
                var v = await _context.LogistiqueVehicules.FindAsync(trajet.VehiculeId);
                if (v != null) v.Statut = "Disponible";

                await _context.SaveChangesAsync();
            }
        }
        #endregion
    }
}
