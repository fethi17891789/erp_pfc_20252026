// Fichier : Metier/Achats/AchatsEmailPollingService.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Metier.Achats
{
    /// <summary>
    /// Service d'arrière-plan qui poll la boîte Gmail toutes les 2 minutes
    /// pour détecter les réponses fournisseur (CONFIRMER / REFUSER) et mettre
    /// à jour automatiquement le statut des bons de commande.
    /// </summary>
    public class AchatsEmailPollingService : BackgroundService
    {
        private readonly IServiceScopeFactory              _scopeFactory;
        private readonly ILogger<AchatsEmailPollingService> _logger;

        private static readonly TimeSpan Intervalle = TimeSpan.FromMinutes(2);

        public AchatsEmailPollingService(
            IServiceScopeFactory scopeFactory,
            ILogger<AchatsEmailPollingService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Polling email démarré (intervalle : {i} min).", Intervalle.TotalMinutes);

            // Premier cycle après 30 secondes (laisser l'app démarrer complètement)
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TraiterReponsesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Polling email : erreur non critique — {msg}", ex.Message);
                }

                await Task.Delay(Intervalle, stoppingToken);
            }
        }

        private async Task TraiterReponsesAsync()
        {
            using var scope       = _scopeFactory.CreateScope();
            var gmailService      = scope.ServiceProvider.GetRequiredService<AchatsGmailService>();
            var achatsService     = scope.ServiceProvider.GetRequiredService<AchatsService>();

            // Gmail non configuré → rien à faire
            if (!await gmailService.EstConfigureAsync()) return;

            var reponses = await gmailService.ChercherReponsesAsync();
            if (reponses.Count == 0) return;

            _logger.LogInformation("Polling email : {n} réponse(s) fournisseur détectée(s).", reponses.Count);

            foreach (var (numeroBc, confirme, messageId) in reponses)
            {
                bool traite = await achatsService.TraiterReponseEmailAsync(numeroBc, confirme);

                if (traite)
                    _logger.LogInformation(
                        "BC {bc} → {statut} (depuis email).",
                        numeroBc, confirme ? "Confirmé" : "Refusé");
                else
                    _logger.LogWarning(
                        "BC {bc} introuvable ou déjà traité — email ignoré.", numeroBc);

                // Marquer l'email comme lu dans tous les cas pour éviter de le retraiter
                await gmailService.MarquerLuAsync(messageId);
            }
        }
    }
}
