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

            Console.WriteLine($"[DEBUG] ErpConfigStorage.ctor - BaseDirectory = {basePath}");
            Console.WriteLine($"[DEBUG] ErpConfigStorage.ctor - Chemin config = {_configFilePath}");
        }

        public ErpConfig Load()
        {
            try
            {
                Console.WriteLine($"[DEBUG] ErpConfigStorage.Load - chemin fichier : {_configFilePath}");

                if (!File.Exists(_configFilePath))
                {
                    Console.WriteLine("[DEBUG] ErpConfigStorage.Load - fichier INEXISTANT, retour config vide.");
                    return new ErpConfig();
                }

                var json = File.ReadAllText(_configFilePath);
                Console.WriteLine($"[DEBUG] ErpConfigStorage.Load - contenu brut : {json}");

                var cfg = JsonSerializer.Deserialize<ErpConfig>(json);

                Console.WriteLine($"[DEBUG] ErpConfigStorage.Load - ConnectionString lue : {cfg?.ConnectionString}");

                return cfg ?? new ErpConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] ErpConfigStorage.Load - exception : {ex}");
                // En cas de problème de lecture, on retourne une config vide
                return new ErpConfig();
            }
        }

        public void Save(ErpConfig config)
        {
            try
            {
                Console.WriteLine($"[DEBUG] ErpConfigStorage.Save - chemin fichier : {_configFilePath}");
                Console.WriteLine($"[DEBUG] ErpConfigStorage.Save - ConnectionString à écrire : {config?.ConnectionString}");

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_configFilePath, json);

                Console.WriteLine("[DEBUG] ErpConfigStorage.Save - fichier écrit avec succès.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] ErpConfigStorage.Save - exception : {ex}");
                // Pour le PFC on ignore les erreurs d'écriture
            }
        }
    }
}
