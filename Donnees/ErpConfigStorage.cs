// Fichier : Donnees/ErpConfigStorage.cs
using System;
using System.IO;
using System.Text.Json;

namespace Donnees
{
    public class ErpConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
    }

    /// <summary>
    /// Gère la lecture / écriture de la config ERP dans un fichier JSON.
    /// </summary>
    public class ErpConfigStorage
    {
        private readonly string _configFilePath;

        public ErpConfigStorage()
        {
            // Fichier erpconfig.json dans le dossier de l'application publiée
            var basePath = AppContext.BaseDirectory;
            _configFilePath = Path.Combine(basePath, "erpconfig.json");
        }

        public ErpConfig Load()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return new ErpConfig();

                var json = File.ReadAllText(_configFilePath);
                var cfg = JsonSerializer.Deserialize<ErpConfig>(json);
                return cfg ?? new ErpConfig();
            }
            catch
            {
                // En cas de problème de lecture, on retourne une config vide
                return new ErpConfig();
            }
        }

        public void Save(ErpConfig config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_configFilePath, json);
            }
            catch
            {
                // Pour le PFC on ignore les erreurs d'écriture
            }
        }
    }
}
