using Microsoft.AspNetCore.SignalR;
using Metier.Logistique;
using System.Threading.Tasks;
using System.Collections.Generic;
using Donnees.Logistique;

namespace Metier.Logistique
{
    public class LogistiqueHub : Hub
    {
        private readonly LogistiqueService _logistiqueService;
        
        // Mapping statique : connexionId → (vehiculeId, trajetId)
        // Permet de clôturer le bon trajet si le chauffeur ferme l'appli (OnDisconnected)
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int vehiculeId, int trajetId)> _connectionMap
            = new System.Collections.Concurrent.ConcurrentDictionary<string, (int vehiculeId, int trajetId)>();

        public LogistiqueHub(LogistiqueService logistiqueService)
        {
            _logistiqueService = logistiqueService;
        }

        public async Task UpdatePosition(int vehiculeId, double lat, double lon)
        {
            // 1. Mettre à jour en base de données
            await _logistiqueService.UpdatePositionVehiculeAsync(vehiculeId, lat, lon);

            // 2. Diffuser aux autres clients (la carte)
            await Clients.Others.SendAsync("ReceivePositionUpdate", vehiculeId, lat, lon);
        }

        public async Task<int> StartTrajet(int vehiculeId, string deviceId, string origine = "")
        {
            var capteur = await _logistiqueService.GetOrCreateCapteurByUidAsync(deviceId ?? "web-tracker");
            var trajet = await _logistiqueService.StartTrajetAsync(vehiculeId, capteur.Id, origine);

            // Enregistrer (vehiculeId, trajetId) pour cette connexion (pour OnDisconnected)
            _connectionMap[Context.ConnectionId] = (vehiculeId, trajet.Id);

            await Clients.Others.SendAsync("TrajetStarted", vehiculeId, trajet.Id, origine, trajet.DateDebut);
            return trajet.Id;
        }

        public async Task UpdatePositionWithTrace(int vehiculeId, int trajetId, double lat, double lon, double speed, string duration, double distanceKm, double dureeArretMinutes = 0)
        {
            await _logistiqueService.UpdatePositionVehiculeAsync(vehiculeId, lat, lon);

            // Calcul CO2 temps réel
            double co2Grammes = 0;
            var vehicule = await _logistiqueService.GetVehiculeByIdAsync(vehiculeId);
            if (vehicule?.EmissionCO2ParKm.HasValue == true)
            {
                co2Grammes = LogistiqueService.CalculerCO2Trajet(
                    distanceKm, dureeArretMinutes,
                    vehicule.EmissionCO2ParKm.Value,
                    vehicule.TypeCarburant,
                    vehicule.TypeTransport);
            }

            // Diffuser avec CO2
            await Clients.Others.SendAsync("ReceivePositionWithTrace", vehiculeId, trajetId, lat, lon, speed, duration, distanceKm, co2Grammes);
        }

        public async Task EndTrajet(int vehiculeId, int trajetId, string destination, double distanceKm, string traceJson, double dureeArretMinutes = 0, string itineraireType = null)
        {
            _connectionMap.TryRemove(Context.ConnectionId, out _); // Trajet clôturé normalement

            // Calcul CO2 final
            double co2Grammes = 0;
            var vehicule = await _logistiqueService.GetVehiculeByIdAsync(vehiculeId);
            if (vehicule?.EmissionCO2ParKm.HasValue == true)
            {
                co2Grammes = LogistiqueService.CalculerCO2Trajet(
                    distanceKm, dureeArretMinutes,
                    vehicule.EmissionCO2ParKm.Value,
                    vehicule.TypeCarburant,
                    vehicule.TypeTransport);
            }

            await _logistiqueService.EndTrajetAsync(trajetId, destination, distanceKm, traceJson, itineraireType);
            await _logistiqueService.UpdateCO2TrajetAsync(trajetId, co2Grammes, dureeArretMinutes);
            await Clients.Others.SendAsync("TrajetEnded", vehiculeId, trajetId, traceJson, distanceKm, co2Grammes);
        }

        public async Task<List<TrajetAvecBlockchainDto>> GetTrajetHistory()
        {
            return await _logistiqueService.GetTrajetsRecentsAvecBlockchainAsync(10);
        }

        public async Task ForceResetVehicule(int vehiculeId)
        {
            await _logistiqueService.ForceResetVehiculeAsync(vehiculeId);
            // Informer tout le monde (y compris l'expéditeur pour MAJ locale)
            await Clients.All.SendAsync("TrajetEnded", vehiculeId, 0, "[]", 0);
        }

        public override async Task OnDisconnectedAsync(System.Exception exception)
        {
            if (_connectionMap.TryRemove(Context.ConnectionId, out var info))
            {
                // Le chauffeur a perdu sa connexion ou fermé son navigateur
                // On libère le véhicule et clôture le trajet en base
                await _logistiqueService.ForceResetVehiculeAsync(info.vehiculeId);

                // Diffuser avec le vrai trajetId (pas 0) pour que les dashboards mettent à jour correctement
                await Clients.All.SendAsync("TrajetEnded", info.vehiculeId, info.trajetId, "[]", 0);

                System.Console.WriteLine($"[SIGNALR DISCONNECT] Véhicule {info.vehiculeId} / Trajet {info.trajetId} libérés automatiquement.");
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinVehiculeGroup(int vehiculeId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Vehicule_{vehiculeId}");
        }
    }
}
