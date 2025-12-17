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

        public string? ResultMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
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
                Password = Password, // à remplacer plus tard par un hash
                LogoFileName = logoFileName
            };

            _db.ErpUsers.Add(user);
            await _db.SaveChangesAsync();

            // Rediriger vers la page de connexion avec l'email pré-rempli
            return RedirectToPage("/Login", new { email = Email });
        }
    }
}
