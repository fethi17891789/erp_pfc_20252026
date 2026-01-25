// Fichier : Presentation/view/Messagerie.cshtml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Donnees;
using Metier.Messagerie;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace erp_pfc_20252026.Pages
{
    public class MessagerieModel : PageModel
    {
        private readonly ErpDbContext _context;
        private readonly MessagerieService _messagerieService;
        private readonly IWebHostEnvironment _env;

        public MessagerieModel(ErpDbContext context, MessagerieService messagerieService, IWebHostEnvironment env)
        {
            _context = context;
            _messagerieService = messagerieService;
            _env = env;
        }

        public class ChatUserViewModel
        {
            public int Id { get; set; }
            public string Login { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Poste { get; set; } = string.Empty;
        }

        public IList<ChatUserViewModel> Utilisateurs { get; set; } = new List<ChatUserViewModel>();

        // ID utilisateur courant (pour la page et le JS)
        public int CurrentUserId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Récupérer l'ID utilisateur depuis la session
            var sessionUserId = HttpContext.Session.GetInt32("CurrentUserId");
            if (sessionUserId == null || sessionUserId.Value <= 0)
            {
                // Pas connecté -> retour à la page de login
                return RedirectToPage("/Login");
            }

            CurrentUserId = sessionUserId.Value;

            var allUsers = await _context.ErpUsers
                .OrderBy(u => u.Login)
                .ToListAsync();

            Utilisateurs = allUsers
                .Select(u => new ChatUserViewModel
                {
                    Id = u.Id,
                    Login = u.Login ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    Poste = u.Poste ?? string.Empty
                })
                .ToList();

            return Page();
        }

        // Handler appelé par /Messagerie?handler=Conversation&otherUserId=x
        public async Task<IActionResult> OnGetConversationAsync(int otherUserId)
        {
            var sessionUserId = HttpContext.Session.GetInt32("CurrentUserId");
            if (sessionUserId == null || sessionUserId.Value <= 0)
            {
                return BadRequest("Utilisateur non connecté.");
            }

            var currentUserId = sessionUserId.Value;

            var conv = await _messagerieService
                .GetOrCreateDirectConversationAsync(currentUserId, otherUserId);

            return new JsonResult(conv);
        }

        // Upload d'un message audio (POST /Messagerie?handler=UploadAudio)
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostUploadAudioAsync()
        {
            var sessionUserId = HttpContext.Session.GetInt32("CurrentUserId");
            if (sessionUserId == null || sessionUserId.Value <= 0)
            {
                return BadRequest("Utilisateur non connecté.");
            }

            var form = HttpContext.Request.Form;

            if (!int.TryParse(form["conversationId"], out var conversationId) || conversationId <= 0)
            {
                return BadRequest("conversationId invalide.");
            }

            var audioFile = form.Files["audioFile"];
            if (audioFile == null || audioFile.Length == 0)
            {
                return BadRequest("Fichier audio manquant.");
            }

            // Dossier physique pour stocker les audios
            var uploadsRoot = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "audio");
            if (!Directory.Exists(uploadsRoot))
            {
                Directory.CreateDirectory(uploadsRoot);
            }

            var ext = Path.GetExtension(audioFile.FileName);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".webm";
            }

            var fileName = $"audio_{conversationId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
            var filePath = Path.Combine(uploadsRoot, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await audioFile.CopyToAsync(stream);
            }

            var fileUrl = $"/uploads/audio/{fileName}";

            var messageDto = await _messagerieService.SaveAudioMessageAsync(
                conversationId,
                sessionUserId.Value,
                fileUrl
            );

            return new JsonResult(messageDto);
        }
    }
}
