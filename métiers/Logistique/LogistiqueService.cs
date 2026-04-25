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
        private readonly Metier.BlockchainService _blockchain;
        private readonly Metier.IAService _iaService;
        private static readonly HttpClient _httpClient = new HttpClient();

        public LogistiqueService(ErpDbContext context, IConfiguration config, Metier.BlockchainService blockchain, Metier.IAService iaService)
        {
            _context = context;
            _config = config;
            _blockchain = blockchain;
            _iaService = iaService;
        }

        #region Maintenance
        public async Task CleanupAbandonedTrajetsAsync(int timeoutMinutes = 15)
        {
            try {
                var limit = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-timeoutMinutes), DateTimeKind.Utc);
                
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

        public async Task<Vehicule?> UpdateVehiculeAsync(int id, string nom, string matricule, string type,
            string? marque, string? modele, int? annee, string? carburant)
        {
            var v = await _context.LogistiqueVehicules.FindAsync(id);
            if (v == null) return null;

            v.Nom           = nom;
            v.Matricule     = matricule;
            v.TypeTransport = type;
            v.Marque        = marque;
            v.Modele        = modele;
            v.Annee         = annee;
            v.TypeCarburant = carburant;
            // Recalcul CO2 immédiat (formule ADEME)
            v.EmissionCO2ParKm = EstimerCO2ParKmFormule(carburant, type, annee);

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
            // Vérifier si un trajet est déjà en cours pour CE véhicule (anti double-clic)
            var trajetExistant = await _context.LogistiqueTrajets
                .Where(t => t.VehiculeId == vehiculeId && t.Statut == "En Cours")
                .OrderByDescending(t => t.DateDebut)
                .FirstOrDefaultAsync();

            if (trajetExistant != null)
            {
                Console.WriteLine($"[INFO] StartTrajetAsync - Trajet déjà actif ({trajetExistant.Id}) pour véhicule {vehiculeId}, retour de l'existant.");
                return trajetExistant;
            }

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

        public async Task EndTrajetAsync(int trajetId, string destination = "", double distance = 0, string traceJson = null, string itineraireType = null)
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
                trajet.TraceJson = traceJson;
                if (!string.IsNullOrEmpty(itineraireType)) trajet.ItineraireType = itineraireType;

                var v = await _context.LogistiqueVehicules.FindAsync(trajet.VehiculeId);
                if (v != null) {
                    v.Statut = "Disponible";
                    v.DerniereMiseAJour = DateTime.UtcNow;
                }

                // Première sauvegarde immédiate
                await _context.SaveChangesAsync();
                Console.WriteLine($"[DEBUG] EndTrajetAsync - Trajet {trajetId} clôturé et véhicule libéré.");

                // Ancrage blockchain du trajet terminé
                var refTrajet = $"TRAJET-{trajetId}-{trajet.DateDebut:yyyyMMddHHmmss}";
                var contenuTrajet = System.Text.Encoding.UTF8.GetBytes(
                    $"{refTrajet}|{destination}|{distance}|{trajet.DateDebut:O}|{trajet.DateFin:O}|{traceJson ?? ""}");
                await _blockchain.AncrerDocumentAsync("TRAJET", refTrajet, contenuTrajet);

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

        public async Task<List<TrajetAvecBlockchainDto>> GetTrajetsRecentsAvecBlockchainAsync(int limit = 10)
        {
            var trajets = await _context.LogistiqueTrajets
                .OrderByDescending(t => t.DateDebut)
                .Take(limit)
                .ToListAsync();

            var result = new List<TrajetAvecBlockchainDto>();
            foreach (var t in trajets)
            {
                var refTrajet = $"TRAJET-{t.Id}-{t.DateDebut:yyyyMMddHHmmss}";
                var ancrage = await _blockchain.GetAncrageParRefAsync(refTrajet);
                result.Add(new TrajetAvecBlockchainDto
                {
                    Id                  = t.Id,
                    VehiculeId          = t.VehiculeId,
                    DateDebut           = t.DateDebut,
                    Origine             = t.Origine,
                    Destination         = t.Destination,
                    DistanceParcourueKm = t.DistanceParcourueKm,
                    Co2EmisGrammes      = t.Co2EmisGrammes,
                    TraceJson           = t.TraceJson,
                    StatutBlockchain    = ancrage?.Statut ?? "EnAttente",
                    LienEtherscan       = ancrage?.LienEtherscan,
                    HashContenu         = ancrage?.HashContenu
                });
            }
            return result;
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

        #region CO2 & RSE

        /// <summary>
        /// Formule ADEME : estimation g CO2/km selon carburant, type véhicule, année.
        /// Résultat instantané, sans appel réseau.
        /// </summary>
        public static double EstimerCO2ParKmFormule(string? typeCarburant, string? typeTransport, int? annee)
        {
            double baseVal = (typeCarburant ?? "").ToLower() switch
            {
                "électrique" or "electrique" or "electric" or "ev" => 20,
                "hybride rechargeable" or "phev" or "plug-in hybride" => 55,
                "hybride" or "hybrid" or "hev" or "mild hybrid" => 105,
                "gnv" or "gpl" or "gaz" or "cng" or "lpg" => 120,
                "diesel" => 145,
                _ => 165  // Essence par défaut
            };

            double mult = (typeTransport ?? "").ToLower() switch
            {
                "camion" or "poids lourd" or "truck" => 2.2,
                "fourgonnette" or "van" or "utilitaire" => 1.55,
                "moto" or "scooter" or "moto-cross" => 0.75,
                "drone" => 0.1,
                _ => 1.0
            };

            if (annee.HasValue)
            {
                if      (annee >= 2021) mult *= 0.87;
                else if (annee >= 2016) mult *= 0.96;
                else if (annee <  2010) mult *= 1.18;
            }

            return Math.Round(baseVal * mult, 1);
        }

        /// <summary>
        /// CO2 émis au ralenti (moteur allumé, vitesse ≤ 2 km/h) en g/minute.
        /// </summary>
        public static double CO2RalentiGParMinute(string? typeCarburant, string? typeTransport)
        {
            bool isTruck = (typeTransport ?? "").ToLower() == "camion";
            return (typeCarburant ?? "").ToLower() switch
            {
                "électrique" or "electrique" or "ev" or "electric" => 0,
                "hybride rechargeable" or "phev" => 5,
                "hybride" => isTruck ? 14 : 9,
                "gnv" or "gpl" => isTruck ? 32 : 17,
                "diesel" => isTruck ? 50 : 26,
                _ => isTruck ? 42 : 22  // Essence
            };
        }

        /// <summary>
        /// Calcule le CO2 total d'un trajet en grammes.
        /// </summary>
        public static double CalculerCO2Trajet(double distanceKm, double dureeArretMinutes, double emissionCO2ParKm, string? typeCarburant, string? typeTransport)
        {
            double co2Route   = distanceKm * emissionCO2ParKm;
            double co2Ralenti = dureeArretMinutes * CO2RalentiGParMinute(typeCarburant, typeTransport);
            return Math.Round(co2Route + co2Ralenti, 0);
        }

        /// <summary>
        /// Appelle Gemini pour affiner l'estimation CO2 du véhicule.
        /// Retourne la valeur affinée ou la formule en cas d'échec.
        /// </summary>
        public async Task<double> EstimerCO2ParKmAvecIAAsync(string marque, string modele, int? annee, string? typeCarburant, string? typeTransport)
        {
            double valeurFormule = EstimerCO2ParKmFormule(typeCarburant, typeTransport, annee);
            try
            {
                var systemPrompt = "Tu es une base de données technique sur les émissions CO2 des véhicules. " +
                    "Réponds UNIQUEMENT en JSON valide avec un seul champ 'co2_g_km' (nombre décimal, grammes CO2 par kilomètre en cycle mixte WLTP ou NEDC). " +
                    "Si tu ne connais pas le modèle exact, fournis la meilleure estimation basée sur la catégorie et l'année. " +
                    "Ne fournis aucun texte en dehors du JSON.";

                var anneeStr = annee.HasValue ? $"Année: {annee}" : "";
                var userPrompt = $"Véhicule: {marque} {modele}. Type: {typeTransport}. Carburant: {typeCarburant}. {anneeStr}. " +
                    $"Quelle est l'émission CO2 en g/km (cycle mixte) ? Retourne uniquement: {{\"co2_g_km\": <valeur>}}";

                var (json, _) = await _iaService.AskAiJsonAsync(systemPrompt, userPrompt);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("co2_g_km", out var co2El))
                {
                    double iaVal = co2El.GetDouble();
                    // Sanity check : valeur raisonnable entre 0 et 1000 g/km
                    if (iaVal > 0 && iaVal < 1000)
                    {
                        Console.WriteLine($"[RSE IA] CO2 estimé par Gemini : {iaVal} g/km pour {marque} {modele}");
                        return Math.Round(iaVal, 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RSE IA] Fallback formule ADEME (erreur Gemini : {ex.Message})");
            }
            return valeurFormule;
        }

        /// <summary>
        /// Met à jour uniquement l'émission CO2/km d'un véhicule (utilisé après affinage IA).
        /// </summary>
        public async Task UpdateCO2VehiculeAsync(int id, double co2)
        {
            var v = await _context.LogistiqueVehicules.FindAsync(id);
            if (v != null)
            {
                v.EmissionCO2ParKm = co2;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Met à jour les données CO2 d'un trajet en cours.
        /// </summary>
        public async Task UpdateCO2TrajetAsync(int trajetId, double co2Grammes, double dureeArretMinutes)
        {
            var trajet = await _context.LogistiqueTrajets.FindAsync(trajetId);
            if (trajet != null)
            {
                trajet.Co2EmisGrammes = co2Grammes;
                trajet.DureeArretMinutes = dureeArretMinutes;
                await _context.SaveChangesAsync();
            }
        }

        // ── DTO interne pour désérialiser le tracé GPS ───────────────
        private sealed class TracePoint
        {
            [System.Text.Json.Serialization.JsonPropertyName("lat")]
            public double Lat { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("lon")]
            public double Lon { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
            public long Timestamp { get; set; }
        }

        private static double HaversineMetres(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6_371_000;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                     * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        /// <summary>
        /// Phase 3 VSP : recalcule le CO2 d'un trajet terminé en intégrant
        /// la pente réelle via Open-Elevation API (gratuit, sans clé).
        /// Non-bloquant — met à jour la BDD si le résultat est cohérent.
        /// </summary>
        public async Task RecalculerCO2AvecElevationAsync(
            int trajetId, string traceJson,
            double emissionCO2ParKm, string? typeCarburant, string? typeTransport)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(traceJson) || traceJson == "[]") return;

                var points = JsonSerializer.Deserialize<List<TracePoint>>(traceJson);
                if (points == null || points.Count < 2) return;

                // ── 1. Récupération des élévations (batch 100 points) ────────
                double[] elevations = new double[points.Count];
                const int BATCH = 100;

                for (int i = 0; i < points.Count; i += BATCH)
                {
                    var slice = points.Skip(i).Take(BATCH).ToList();
                    var body = JsonSerializer.Serialize(new
                    {
                        locations = slice.Select(p => new { latitude = p.Lat, longitude = p.Lon })
                    });
                    var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(20));
                    var resp = await _httpClient.PostAsync(
                        "https://api.open-elevation.com/api/v1/lookup", content, cts.Token);
                    resp.EnsureSuccessStatusCode();

                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    var results = doc.RootElement.GetProperty("results").EnumerateArray().ToList();
                    for (int j = 0; j < results.Count && (i + j) < points.Count; j++)
                        elevations[i + j] = results[j].GetProperty("elevation").GetDouble();
                }

                // ── 2. Calcul VSP + pente segment par segment ────────────────
                double totalCO2G   = 0;
                double prevSpeedMs = 0;
                double base_       = emissionCO2ParKm > 0 ? emissionCO2ParKm : 150;
                const double vRef  = 13.89;
                double vspRef      = vRef * 0.132 + 0.000302 * Math.Pow(vRef, 3); // ≈ 2.64
                double baseRateGps = base_ * vRef / 1000;

                for (int i = 1; i < points.Count; i++)
                {
                    double deltaT = (points[i].Timestamp - points[i - 1].Timestamp) / 1000.0;
                    if (deltaT <= 0 || deltaT > 120) { prevSpeedMs = 0; continue; }

                    double distM    = HaversineMetres(points[i-1].Lat, points[i-1].Lon, points[i].Lat, points[i].Lon);
                    double speedMps = distM / deltaT;
                    if (speedMps > 55) { prevSpeedMs = 0; continue; } // clip 200 km/h

                    double grade    = distM > 1 ? (elevations[i] - elevations[i-1]) / distM : 0;
                    grade           = Math.Max(-0.15, Math.Min(0.15, grade)); // clip ±15 %

                    double accMps2  = (speedMps - prevSpeedMs) / deltaT;
                    double vsp      = speedMps * (1.1 * accMps2 + 9.81 * grade + 0.132)
                                    + 0.000302 * Math.Pow(speedMps, 3);

                    double dynFactor;
                    if (speedMps < 0.5)
                        dynFactor = base_ * 0.0021 / baseRateGps; // ralenti
                    else if (vsp <= 0)
                        dynFactor = 0.08;                          // frein moteur
                    else
                        dynFactor = Math.Max(0.08, Math.Min(vsp / vspRef, 4.0));

                    totalCO2G  += baseRateGps * dynFactor * deltaT;
                    prevSpeedMs = speedMps;
                }

                // ── 3. Mise à jour BDD si cohérent ──────────────────────────
                if (totalCO2G > 0)
                {
                    var trajet = await _context.LogistiqueTrajets.FindAsync(trajetId);
                    if (trajet != null)
                    {
                        trajet.Co2EmisGrammes = Math.Round(totalCO2G, 0);
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"[VSP+ELEVATION] Trajet {trajetId} CO2 affiné : {Math.Round(totalCO2G / 1000, 3)} kg");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VSP+ELEVATION] Recalcul trajet {trajetId} ignoré : {ex.Message}");
                // Non-bloquant : l'estimation initiale reste en base
            }
        }

        /// <summary>
        /// Statistiques RSE pour le dashboard environnement.
        /// </summary>
        public async Task<StatistiquesRSE> GetStatistiquesRSEAsync()
        {
            var vehicules = await _context.LogistiqueVehicules.OrderBy(v => v.Nom).ToListAsync();
            var now = DateTime.UtcNow;
            var debutMois = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1), DateTimeKind.Utc);
            var trajets = await _context.LogistiqueTrajets
                .Where(t => t.Statut == "Termine" && t.DateDebut >= debutMois)
                .ToListAsync();

            var stats = new StatistiquesRSE
            {
                Vehicules = vehicules,
                TotalCO2KgCeMois = Math.Round(trajets.Sum(t => t.Co2EmisGrammes) / 1000.0, 2),
                NombreTrajets = trajets.Count,
                DistanceTotaleKm = Math.Round(trajets.Sum(t => t.DistanceParcourueKm), 1)
            };

            // CO2 par véhicule ce mois
            stats.CO2ParVehicule = vehicules.Select(v => new VehiculeRSEStat
            {
                VehiculeId = v.Id,
                Nom = v.Nom,
                TypeCarburant = v.TypeCarburant ?? "Inconnu",
                EmissionCO2ParKm = v.EmissionCO2ParKm ?? 0,
                CO2TotalKgCeMois = Math.Round(
                    trajets.Where(t => t.VehiculeId == v.Id).Sum(t => t.Co2EmisGrammes) / 1000.0, 2),
                NombreTrajets = trajets.Count(t => t.VehiculeId == v.Id),
                DistanceTotaleKm = Math.Round(trajets.Where(t => t.VehiculeId == v.Id).Sum(t => t.DistanceParcourueKm), 1)
            }).OrderByDescending(x => x.CO2TotalKgCeMois).ToList();

            // Véhicule le + polluant
            stats.VehiculeLesPollant = stats.CO2ParVehicule.FirstOrDefault();

            // Économies potentielles : si on remplace les thermiques par l'électrique moyen (20 g/km)
            var trajetsThermiques = trajets
                .Join(vehicules.Where(v => v.TypeCarburant != null &&
                      !v.TypeCarburant.ToLower().Contains("electr") &&
                      !v.TypeCarburant.ToLower().Contains("électr")),
                      t => t.VehiculeId, v => v.Id, (t, v) => new { t, v })
                .ToList();
            double co2ThermiqueActuel = trajetsThermiques.Sum(x => x.t.Co2EmisGrammes);
            double co2SiElectrique = trajetsThermiques.Sum(x => x.t.DistanceParcourueKm * 20);
            stats.EconomiesPotentiellesKg = Math.Round(Math.Max(0, co2ThermiqueActuel - co2SiElectrique) / 1000.0, 2);

            // Données mensuelles — historique infini (du premier trajet enregistré jusqu'au mois en cours)
            var tousLesTrajetsMensuels = await _context.LogistiqueTrajets
                .Where(t => t.Statut == "Termine")
                .OrderBy(t => t.DateDebut)
                .ToListAsync();

            stats.DonneesMensuelles = new List<MoisRSE>();
            var culture = new System.Globalization.CultureInfo("fr-FR");
            var moisCourant = new DateTime(now.Year, now.Month, 1);

            if (tousLesTrajetsMensuels.Any())
            {
                var premierTrajet = tousLesTrajetsMensuels.First().DateDebut;
                var moisDepart = new DateTime(premierTrajet.Year, premierTrajet.Month, 1);
                var curseur = moisDepart;
                while (curseur <= moisCourant)
                {
                    var debutMoisI = DateTime.SpecifyKind(curseur, DateTimeKind.Utc);
                    var finMoisI   = DateTime.SpecifyKind(curseur.AddMonths(1), DateTimeKind.Utc);
                    var trajetsMois = tousLesTrajetsMensuels
                        .Where(t => t.DateDebut >= debutMoisI && t.DateDebut < finMoisI)
                        .ToList();
                    stats.DonneesMensuelles.Add(new MoisRSE
                    {
                        Label      = curseur.ToString("MMM yyyy", culture),
                        CO2TotalKg = Math.Round(trajetsMois.Sum(t => t.Co2EmisGrammes) / 1000.0, 2),
                        DistanceKm = Math.Round(trajetsMois.Sum(t => t.DistanceParcourueKm), 1)
                    });
                    curseur = curseur.AddMonths(1);
                }
            }
            else
            {
                // Aucun trajet : afficher uniquement le mois en cours avec zéro
                stats.DonneesMensuelles.Add(new MoisRSE
                {
                    Label      = moisCourant.ToString("MMM yyyy", culture),
                    CO2TotalKg = 0,
                    DistanceKm = 0
                });
            }

            return stats;
        }

        public async Task<List<Trajet>> GetTrajetsAvecCO2Async(int limit = 20)
        {
            return await _context.LogistiqueTrajets
                .Where(t => t.Statut == "Termine")
                .OrderByDescending(t => t.DateDebut)
                .Take(limit)
                .ToListAsync();
        }

        #endregion
    }

    // DTOs pour les statistiques RSE
    public class StatistiquesRSE
    {
        public List<Vehicule> Vehicules { get; set; } = new();
        public double TotalCO2KgCeMois { get; set; }
        public int NombreTrajets { get; set; }
        public double DistanceTotaleKm { get; set; }
        public double EconomiesPotentiellesKg { get; set; }
        public List<VehiculeRSEStat> CO2ParVehicule { get; set; } = new();
        public VehiculeRSEStat? VehiculeLesPollant { get; set; }
        public List<MoisRSE> DonneesMensuelles { get; set; } = new();
    }

    public class VehiculeRSEStat
    {
        public int VehiculeId { get; set; }
        public string Nom { get; set; } = "";
        public string TypeCarburant { get; set; } = "";
        public double EmissionCO2ParKm { get; set; }
        public double CO2TotalKgCeMois { get; set; }
        public int NombreTrajets { get; set; }
        public double DistanceTotaleKm { get; set; }
    }

    public class MoisRSE
    {
        public string Label { get; set; } = "";
        public double CO2TotalKg { get; set; }
        public double DistanceKm { get; set; }
    }

    public class TrajetAvecBlockchainDto
    {
        public int Id { get; set; }
        public int VehiculeId { get; set; }
        public DateTime DateDebut { get; set; }
        public string? Origine { get; set; }
        public string? Destination { get; set; }
        public double DistanceParcourueKm { get; set; }
        public double Co2EmisGrammes { get; set; }
        public string? TraceJson { get; set; }
        public string StatutBlockchain { get; set; } = "EnAttente";
        public string? LienEtherscan { get; set; }
        public string? HashContenu { get; set; }
    }
}
