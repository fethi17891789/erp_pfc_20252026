using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Donnees;
using Donnees.Logistique;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Metier.Logistique
{
    public class LogistiqueService
    {
        private readonly ErpDbContext _context;
        private readonly IConfiguration _config;
        private static readonly HttpClient _httpClient = new HttpClient();

        public LogistiqueService(ErpDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        #region Maintenance
        public async Task CleanupAbandonedTrajetsAsync(int timeoutMinutes = 15)
        {
            try {
                var limit = DateTime.UtcNow.AddMinutes(-timeoutMinutes);
                
                // 1. Chercher les véhicules "En Trajet" qui n'ont pas bougé depuis X minutes
                var stagnantVehicules = await _context.LogistiqueVehicules
                    .Where(v => v.Statut == "En Trajet" && v.DerniereMiseAJour < limit)
                    .ToListAsync();

                if (stagnantVehicules.Any()) {
                    foreach(var v in stagnantVehicules) {
                        await InternalResetVehiculeAsync(v.Id);
                    }
                    Console.WriteLine($"[AUTO-CLEANUP] {stagnantVehicules.Count} véhicules libérés pour inactivité.");
                }
            } catch(Exception ex) {
                Console.WriteLine($"[AUTO-CLEANUP ERROR] {ex.Message}");
            }
        }

        public async Task ForceResetVehiculeAsync(int vehiculeId)
        {
            await InternalResetVehiculeAsync(vehiculeId);
        }

        private async Task InternalResetVehiculeAsync(int vehiculeId)
        {
            var v = await _context.LogistiqueVehicules.FindAsync(vehiculeId);
            if (v != null) {
                v.Statut = "Disponible";
                v.DerniereMiseAJour = DateTime.UtcNow;
                
                // Terminer aussi le trajet associé en DB s'il existe
                var activeTrajet = await _context.LogistiqueTrajets
                    .Where(t => t.VehiculeId == v.Id && t.Statut == "En Cours")
                    .OrderByDescending(t => t.DateDebut)
                    .FirstOrDefaultAsync();

                if (activeTrajet != null) {
                    activeTrajet.Statut = "Termine (Reset)";
                    activeTrajet.DateFin = DateTime.UtcNow;
                }
                
                await _context.SaveChangesAsync();
            }
        }
        #endregion

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
            try {
                var trajet = await _context.LogistiqueTrajets.FindAsync(trajetId);
                if (trajet == null) {
                    Console.WriteLine($"[AVERTISSEMENT] EndTrajetAsync - Trajet {trajetId} non trouvé.");
                    return;
                }

                // 1. CRITICAL : Mettre à jour les statuts en priorité pour débloquer l'interface
                trajet.DateFin = DateTime.UtcNow;
                trajet.Destination = destination;
                trajet.DistanceParcourueKm = distance;
                trajet.Statut = "Termine";
                trajet.TraceJson = traceJson; // On garde le brut en attendant le matching

                var v = await _context.LogistiqueVehicules.FindAsync(trajet.VehiculeId);
                if (v != null) {
                    v.Statut = "Disponible";
                    v.DerniereMiseAJour = DateTime.UtcNow;
                }

                // Première sauvegarde immédiate
                await _context.SaveChangesAsync();
                Console.WriteLine($"[DEBUG] EndTrajetAsync - Trajet {trajetId} clôturé et véhicule libéré.");

                // 2. OPTIONNEL : Tenter le Map Matching (OSRM)
                if (!string.IsNullOrEmpty(traceJson) && traceJson != "[]") {
                    try {
                        var matchedTrace = await ProcessMapMatchingAsync(traceJson);
                        if (!string.IsNullOrEmpty(matchedTrace) && matchedTrace != traceJson) {
                            trajet.TraceJson = matchedTrace;
                            await _context.SaveChangesAsync();
                            Console.WriteLine($"[DEBUG] EndTrajetAsync - Trajet {trajetId} : Map-Matching réussi.");
                        }
                    } catch (Exception ex) {
                        Console.WriteLine($"[INFO] Map-Matching ignoré (non bloquant) : {ex.Message}");
                    }
                }
            } catch(Exception ex) {
                Console.WriteLine($"[ERREUR CRITIQUE] EndTrajetAsync: {ex.Message}");
                throw; // SignalR remontera l'erreur au client mobile
            }
        }

        public async Task<List<Trajet>> GetTrajetsRecentsAsync(int limit = 10)
        {
            return await _context.LogistiqueTrajets
                .OrderByDescending(t => t.DateDebut)
                .Take(limit)
                .ToListAsync();
        }

        private async Task<string> ProcessMapMatchingAsync(string rawTraceJson)
        {
            try
            {
                // Parser le JSON brut venu du client : [{"lat":48.8, "lon":2.3}, ...]
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var points = JsonSerializer.Deserialize<List<GpsPoint>>(rawTraceJson, options);
                if (points == null || points.Count < 2) return rawTraceJson;

                // Format OSRM: lon,lat;lon,lat;...
                // OSRM map matching public accepte max 100 coordonnees dans l'URL. Le frontend a dejà du sous-échantillonner.
                var coordinates = string.Join(";", points.Select(p => 
                    $"{p.Lon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{p.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                ));

                // URL publique OSRM (Expérimental/Démonstration)
                var url = $"http://router.project-osrm.org/match/v1/driving/{coordinates}?generate_hints=false&geometries=geojson&overview=full";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    // Extraire la geometry du premier matching OSRM (compatible GeoJSON LineString)
                    using var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.GetProperty("code").GetString() == "Ok")
                    {
                        var matchings = doc.RootElement.GetProperty("matchings");
                        if (matchings.GetArrayLength() > 0)
                        {
                            var geometry = matchings[0].GetProperty("geometry");
                            return geometry.ToString(); // Retourne un GeoJSON LineString
                        }
                    }
                }
                
                // Fallback si l'API retourne une erreur (ex: trajet hors route, ou limitation de fréquence OSRM)
                return rawTraceJson; 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur Map Matching OSRM: {ex.Message}");
                return rawTraceJson; // Fallback GPS brut
            }
        }

        private class GpsPoint
        {
            [System.Text.Json.Serialization.JsonPropertyName("lat")]
            public double Lat { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("lon")]
            public double Lon { get; set; }
        }
        #endregion
    }
}
