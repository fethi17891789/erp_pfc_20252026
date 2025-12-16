// Fichier : Pages/Presentation/view/BDDView.cshtml.cs
using System;
using System.Threading.Tasks;
using Donnees;
using Metier;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages
{
    public class BDDViewModel : PageModel
    {
        private readonly BDDService _bddService;
        private readonly DynamicConnectionProvider _connectionProvider;

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

        public BDDViewModel(BDDService bddService, DynamicConnectionProvider connectionProvider)
        {
            _bddService = bddService;
            _connectionProvider = connectionProvider;
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

                // Création de la base ERP
                var created = await _bddService.CreateDatabaseIfNotExistsAsync(config);

                // Construire la connection string de cette base
                var conn = $"Host={config.Host};Port={config.Port};Database={config.DatabaseName};Username={config.UserName};Password={config.Password}";

                // La stocker dans le provider pour tout le reste de l’appli
                _connectionProvider.CurrentConnectionString = conn;

                IsSuccess = true;
                ResultMessage = created
                    ? $"Succès : La base de données \"{DatabaseName}\" a été créée."
                    : $"Info : La base de données \"{DatabaseName}\" existe déjà.";

                // Redirection vers la page de choix de profil
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
