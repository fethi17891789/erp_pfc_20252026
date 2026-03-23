using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using System;
using System.IO;

namespace erp_pfc_20252026.Pages.Settings
{
    public class MobileAccessModel : PageModel
    {
        public string Message { get; set; }
        public bool IsSuccess { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnPost(string cloudflareToken)
        {
            if (string.IsNullOrWhiteSpace(cloudflareToken))
            {
                Message = "Le jeton fourni est vide. Veuillez réessayer.";
                IsSuccess = false;
                return Page();
            }

            try
            {
                // Tenter d'exécuter la commande cloudflared
                var processInfo = new ProcessStartInfo
                {
                    FileName = "cloudflared.exe",
                    Arguments = $"service install {cloudflareToken.Trim()}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit(10000); // Attendre max 10 secondes
                    
                    if (process.ExitCode == 0)
                    {
                        var stdOut = process.StandardOutput.ReadToEnd();
                        Message = "Tunnel Cloudflare installé avec succès ! Le service fonctionne désormais en arrière-plan. " + 
                                  (string.IsNullOrWhiteSpace(stdOut) ? "" : "Détails: " + stdOut);
                        IsSuccess = true;
                    }
                    else
                    {
                        var stdErr = process.StandardError.ReadToEnd();
                        Message = "Erreur lors de l'installation du tunnel. Assurez-vous d'ouvrir l'éditeur de code (ou Visual Studio) en mode Administrateur. Détails: " + stdErr;
                        IsSuccess = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Message = $"Impossible de lancer cloudflared.exe. Vérifiez qu'il est bien installé et accessible dans les variables d'environnement (PATH). Erreur: {ex.Message}";
                IsSuccess = false;
            }

            return Page();
        }
    }
}
