using Microsoft.AspNetCore.SignalR;
using Metier.Logistique;
using System.Threading.Tasks;

namespace Metier.Logistique
{
    public class LogistiqueHub : Hub
    {
        private readonly LogistiqueService _logistiqueService;

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

        public async Task JoinVehiculeGroup(int vehiculeId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Vehicule_{vehiculeId}");
        }
    }
}
