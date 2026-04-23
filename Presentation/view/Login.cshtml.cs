// Fichier : Pages/Login.cshtml.cs
using System.Threading.Tasks;
using Donnees;
using Isopoh.Cryptography.Argon2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace erp_pfc_20252026.Pages
{
    public class LoginModel : PageModel
    {
        private readonly ErpDbContext _db;

        public LoginModel(ErpDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string? ResultMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ResultMessage = "Email et mot de passe sont obligatoires.";
                return Page();
            }

            var user = await _db.ErpUsers.FirstOrDefaultAsync(u => u.Email == Email);

            if (user == null || !Argon2.Verify(user.Password, Password))
            {
                ResultMessage = "Identifiants incorrects.";
                return Page();
            }

            // Connexion réussie : on garde l'ID et le login en session
            HttpContext.Session.SetInt32("CurrentUserId", user.Id);
            HttpContext.Session.SetString("CurrentUserLogin", user.Login);

            // Redirection vers la page d'accueil ERP
            return RedirectToPage("/Home");
        }
    }
}
