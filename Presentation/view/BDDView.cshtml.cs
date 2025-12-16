// Fichier : Presentation/view/BDDView.cshtml.cs
using System;
using System.Threading.Tasks;
using Donnees;
using Metier;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages
{
    public class BDDViewModel : PageModel
    {
        private readonly BDDService _bddService;
        private readonly DynamicConnectionProvider _connectionProvider;
        private readonly IServiceProvider _serviceProvider;

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
            IServiceProvider serviceProvider)
        {
            _bddService = bddService;
            _connectionProvider = connectionProvider;
            _serviceProvider = serviceProvider;
        }

        public void OnGet()
        {
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

                // 1) Création de la base ERP
                var created = await _bddService.CreateDatabaseIfNotExistsAsync(config);

                // 2) Connection string vers CETTE base
                var conn = $"Host={config.Host};Port={config.Port};Database={config.DatabaseName};Username={config.UserName};Password={config.Password}";
                _connectionProvider.CurrentConnectionString = conn;

                // 3) Créer les tables (ErpUsers, etc.) dans cette base
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
                    await db.Database.EnsureCreatedAsync();
                }

                IsSuccess = true;
                ResultMessage = created
                    ? $"Succès : La base de données \"{DatabaseName}\" a été créée et initialisée."
                    : $"Info : La base de données \"{DatabaseName}\" existait déjà, schéma vérifié.";

                // 4) Redirection vers la suite (création de profil)
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
