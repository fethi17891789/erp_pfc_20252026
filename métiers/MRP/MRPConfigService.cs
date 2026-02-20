// Fichier : Metier/MRP/MRPConfigService.cs
using System;
using System.Threading.Tasks;
using Donnees;
using Microsoft.EntityFrameworkCore;

namespace Metier.MRP
{
    public class MRPConfigService
    {
        private readonly ErpDbContext _db;

        public MRPConfigService(ErpDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Retourne la config si elle existe, sinon null (ne crée rien).
        /// </summary>
        public async Task<MRPConfigModule> GetConfigAsync()
        {
            return await _db.MRPConfigModules.FirstOrDefaultAsync();
        }

        /// <summary>
        /// Retourne la config unique. Si elle n'existe pas, la crée avec des valeurs par défaut.
        /// </summary>
        public async Task<MRPConfigModule> GetOrCreateConfigAsync()
        {
            var cfg = await _db.MRPConfigModules.FirstOrDefaultAsync();
            if (cfg != null)
                return cfg;

            cfg = new MRPConfigModule
            {
                HorizonParDefautJours = 30, // valeur par défaut
                DateCreation = DateTime.UtcNow,
                DateDerniereModification = DateTime.UtcNow,
                CreeParUserId = null,
                ModifieParUserId = null
            };

            _db.MRPConfigModules.Add(cfg);
            await _db.SaveChangesAsync();

            return cfg;
        }

        /// <summary>
        /// Met à jour l'horizon et les métadonnées.
        /// </summary>
        public async Task UpdateHorizonAsync(int newHorizonJours, int? userId = null)
        {
            var cfg = await GetOrCreateConfigAsync();

            cfg.HorizonParDefautJours = newHorizonJours;
            cfg.DateDerniereModification = DateTime.UtcNow;
            cfg.ModifieParUserId = userId;

            await _db.SaveChangesAsync();
        }
    }
}
