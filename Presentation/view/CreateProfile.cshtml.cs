using System;
using System.IO;
using System.Threading.Tasks;
using Donnees;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages
{
    public class CreateProfileModel : PageModel
    {
        private readonly IWebHostEnvironment _env;
        private readonly ErpDbContext _db;

        public CreateProfileModel(IWebHostEnvironment env, ErpDbContext db)
        {
            _env = env;
            _db = db;
        }

        [BindProperty]
        public IFormFile? LogoFile { get; set; }

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Login { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        // Nouveau : poste sélectionné via le champ combiné
        [BindProperty]
        public string SelectedPoste { get; set; } = string.Empty;

        // Nouveau : indique à la vue si c'est le premier utilisateur
        public bool IsFirstUser { get; set; }

        public string? ResultMessage { get; set; }

        public async Task OnGetAsync()
        {
            // Déterminer si aucun utilisateur n'existe encore
            IsFirstUser = !await _db.ErpUsers.AnyAsync();

            if (IsFirstUser)
            {
                // Premier compte → PDG forcé
                SelectedPoste = "PDG";
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Recalculer l'état "premier utilisateur"
            IsFirstUser = !await _db.ErpUsers.AnyAsync();

            if (IsFirstUser)
            {
                // Premier compte : poste forcé en PDG, on ignore ce qui vient du formulaire
                SelectedPoste = "PDG";
            }

            // Validation basique
            if (string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Login) ||
                string.IsNullOrWhiteSpace(Password))
            {
                ResultMessage = "Email, login et mot de passe sont obligatoires.";
                return Page();
            }

            // Vérifier si l'email existe déjà
            var existingEmail = await _db.ErpUsers
                .FirstOrDefaultAsync(u => u.Email == Email);

            if (existingEmail != null)
            {
                ResultMessage = "Cet email est déjà utilisé. Veuillez en choisir un autre.";
                return Page();
            }

            // Vérifier si le login existe déjà
            var existingLogin = await _db.ErpUsers
                .FirstOrDefaultAsync(u => u.Login == Login);

            if (existingLogin != null)
            {
                ResultMessage = "Ce login est déjà utilisé. Veuillez en choisir un autre.";
                return Page();
            }

            // Sauvegarde du logo si présent
            string? logoFileName = null;

            if (LogoFile != null && LogoFile.Length > 0)
            {
                var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "logos");
                Directory.CreateDirectory(uploadsRoot);

                logoFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(LogoFile.FileName);
                var filePath = Path.Combine(uploadsRoot, logoFileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await LogoFile.CopyToAsync(stream);
            }

            // Création de l'utilisateur
            var user = new ErpUser
            {
                Email = Email,
                Login = Login,
                Password = Password, // TODO: passer à un hash plus tard
                LogoFileName = logoFileName,
                Poste = SelectedPoste // Nouveau : enregistrement du poste
            };

            _db.ErpUsers.Add(user);
            await _db.SaveChangesAsync();

            // Rediriger vers la page de connexion avec l'email pré-rempli
            return RedirectToPage("/Login", new { email = Email });
        }
    }
}
