// Fichier : Presentation/view/BDDView.cshtml.cs
using System;
using System.Threading.Tasks;
using Donnees;
using Metier;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace erp_pfc_20252026.Pages
{
    public class BDDViewModel : PageModel
    {
        private readonly BDDService _bddService;
        private readonly DynamicConnectionProvider _connectionProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly ErpConfigStorage _configStorage;

        [BindProperty]
        public string MasterPassword { get; set; } = string.Empty;

        [BindProperty]
        public string DatabaseName { get; set; } = "erp_db";

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string DbPassword { get; set; } = string.Empty;

        [BindProperty]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? ResultMessage { get; set; }
        public bool IsSuccess { get; set; }

        [BindProperty(SupportsGet = true)]
        public string GeneratedPassword { get; set; } = string.Empty;

        public BDDViewModel(
            BDDService bddService,
            DynamicConnectionProvider connectionProvider,
            IServiceProvider serviceProvider,
            ErpConfigStorage configStorage)
        {
            _bddService = bddService;
            _connectionProvider = connectionProvider;
            _serviceProvider = serviceProvider;
            _configStorage = configStorage;
        }

        public void OnGet()
        {
            // 1) Charger la config éventuelle
            var existing = _configStorage.Load();

            // 2) Si une connection string est présente, tester si la BDD existe encore
            if (!string.IsNullOrWhiteSpace(existing.ConnectionString))
            {
                try
                {
                    // Mettre la connexion en mémoire
                    _connectionProvider.CurrentConnectionString = existing.ConnectionString;

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

                        // Test de connexion : si OK, la BDD existe toujours
                        if (db.Database.CanConnect())
                        {
                            // Aller directement vers la page de choix de profil
                            Response.Redirect("/ChooseProfile");
                            return;
                        }
                    }
                }
                catch
                {
                    // Connexion impossible => on traitera comme BDD inexistante
                }

                // Connexion invalide : on réinitialise la config
                _connectionProvider.CurrentConnectionString = string.Empty;
                _configStorage.Save(new ErpConfig { ConnectionString = string.Empty });
            }

            // 3) Cas normal : afficher le formulaire de création BDD
            if (string.IsNullOrEmpty(GeneratedPassword))
            {
                GeneratedPassword = Guid.NewGuid().ToString("N")[..12];
                MasterPassword = GeneratedPassword;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(DatabaseName)) DatabaseName = "erp_db";
            if (string.IsNullOrWhiteSpace(DbPassword)) DbPassword = "fethi1234";

            try
            {
                var config = new BDDEntity
                {
                    Host = "localhost",
                    Port = 5432,
                    DatabaseName = this.DatabaseName,
                    UserName = "openpg",
                    Password = this.DbPassword,
                    MasterPassword = this.MasterPassword,
                    Email = this.Email,
                    PhoneNumber = this.PhoneNumber
                };

                // 1) Création de la base ERP (si pas déjà là)
                var created = await _bddService.CreateDatabaseIfNotExistsAsync(config);

                // 2) Connection string vers CETTE base
                var conn = $"Host={config.Host};Port={config.Port};Database={config.DatabaseName};Username={config.UserName};Password={config.Password}";

                // Sauvegarde en mémoire pour la session actuelle
                _connectionProvider.CurrentConnectionString = conn;

                // 3) Création des tables dans cette base
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
                    await db.Database.EnsureCreatedAsync();
                }

                // 4) Sauvegarde PERSISTANTE dans erpconfig.json
                var erpConfig = new ErpConfig { ConnectionString = conn };
                _configStorage.Save(erpConfig);

                IsSuccess = true;
                ResultMessage = created
                    ? $"Succès : La base de données \"{DatabaseName}\" a été créée et initialisée."
                    : $"Info : La base de données \"{DatabaseName}\" existait déjà, schéma vérifié.";

                // 5) Après création/validation de la BDD, on va vers la page de choix de profil
                return RedirectToPage("/ChooseProfile");
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                ResultMessage = $"Erreur : {ex.Message}. Vérifie que PgAdmin/Postgres est bien lancé.";

                return Page();
            }
        }
    }
}
