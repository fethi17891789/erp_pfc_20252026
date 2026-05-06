// Fichier : Metier/Achats/AchatsGmailService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Donnees;
using Donnees.Achats;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Metier.Achats
{
    /// <summary>
    /// Service d'envoi d'email via Gmail API (OAuth2).
    /// Zéro configuration SMTP pour l'utilisateur — juste un clic "Connecter avec Gmail".
    /// Gère aussi la détection des réponses fournisseur (CONFIRMER / REFUSER) par polling.
    /// </summary>
    public class AchatsGmailService
    {
        private readonly ErpDbContext    _db;
        private readonly IConfiguration _config;

        // ── Clés OAuth2 (à remplir dans appsettings.json) ─────────────────────
        private string ClientId     => _config["GoogleOAuth:ClientId"]     ?? "";
        private string ClientSecret => _config["GoogleOAuth:ClientSecret"] ?? "";

        // gmail.send = envoyer | gmail.modify = marquer lu | email+openid = adresse
        private const string Scope = "https://www.googleapis.com/auth/gmail.send https://www.googleapis.com/auth/gmail.modify email openid";

        // Regex pour extraire le numéro BC et l'action depuis le sujet d'un email
        private static readonly Regex RegexReponse = new(
            @"(BC-\d{4}-\d{3})\s+(CONFIRMER|REFUSER)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public AchatsGmailService(ErpDbContext db, IConfiguration config)
        {
            _db     = db;
            _config = config;
        }

        // ── Vrai si un token Gmail est configuré ───────────────────────────────
        public async Task<bool> EstConfigureAsync()
        {
            return await _db.AchatEmailTokens.AnyAsync(t => t.Provider == "gmail");
        }

        // ── Récupère l'adresse email configurée ───────────────────────────────
        public async Task<string?> GetEmailConfigureAsync()
        {
            var token = await _db.AchatEmailTokens
                .Where(t => t.Provider == "gmail")
                .OrderByDescending(t => t.ConfigureeLe)
                .FirstOrDefaultAsync();
            return token?.EmailAdresse;
        }

        // ── Génère l'URL d'autorisation Google ────────────────────────────────
        public string GenererUrlAutorisation(string redirectUri, string state)
        {
            var flow    = CreerFlow(redirectUri);
            var request = flow.CreateAuthorizationCodeRequest(redirectUri);

            request.State = state;

            if (request is Google.Apis.Auth.OAuth2.Requests.GoogleAuthorizationCodeRequestUrl googleReq)
                googleReq.Prompt = "consent";

            return request.Build().ToString();
        }

        // ── Échange le code contre les tokens et les stocke ───────────────────
        public async Task EchangerCodeEtSauvegarderAsync(string code, string redirectUri)
        {
            var flow    = CreerFlow(redirectUri);
            var reponse = await flow.ExchangeCodeForTokenAsync("user", code, redirectUri, CancellationToken.None);

            string emailAdresse = await ObtenirEmailAdresseAsync(reponse.AccessToken);

            var anciens = _db.AchatEmailTokens.Where(t => t.Provider == "gmail");
            _db.AchatEmailTokens.RemoveRange(anciens);

            _db.AchatEmailTokens.Add(new AchatEmailToken
            {
                Provider     = "gmail",
                EmailAdresse = emailAdresse,
                AccessToken  = reponse.AccessToken,
                RefreshToken = reponse.RefreshToken ?? "",
                ExpiresAt    = DateTime.UtcNow.AddSeconds(reponse.ExpiresInSeconds ?? 3600),
                ConfigureeLe = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        // ── Déconnexion ───────────────────────────────────────────────────────
        public async Task DeconnecterAsync()
        {
            var tokens = _db.AchatEmailTokens.Where(t => t.Provider == "gmail");
            _db.AchatEmailTokens.RemoveRange(tokens);
            await _db.SaveChangesAsync();
        }

        // ── Envoie le BC via Gmail API ────────────────────────────────────────
        public async Task EnvoyerBonCommandeAsync(AchatBonCommande bc, string emailDestinataire)
        {
            var tokenEntite = await _db.AchatEmailTokens
                .Where(t => t.Provider == "gmail")
                .OrderByDescending(t => t.ConfigureeLe)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Gmail non configuré. Connectez votre compte depuis Paramètres → Email.");

            var credential = await ObtenirCredentialAsync(tokenEntite);
            var service    = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName       = "SKYRA ERP"
            });

            string corps = GenererCorpsEmail(bc, tokenEntite.EmailAdresse);
            string sujet = $"Bon de Commande {bc.Numero} — SKYRA ERP";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("SKYRA ERP", tokenEntite.EmailAdresse));
            message.To.Add(MailboxAddress.Parse(emailDestinataire));
            message.Subject = sujet;
            message.Body    = new TextPart(MimeKit.Text.TextFormat.Html) { Text = corps };

            using var stream = new System.IO.MemoryStream();
            await message.WriteToAsync(stream);
            string raw = Convert.ToBase64String(stream.ToArray())
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            await service.Users.Messages.Send(
                new Google.Apis.Gmail.v1.Data.Message { Raw = raw }, "me").ExecuteAsync();
        }

        // ── Polling : cherche les réponses non lues CONFIRMER / REFUSER ───────
        /// <summary>
        /// Interroge la boîte Gmail et retourne la liste des réponses fournisseur
        /// (numéro BC + action) qui n'ont pas encore été traitées.
        /// </summary>
        public async Task<List<(string NumeroBc, bool Confirme, string MessageId)>> ChercherReponsesAsync()
        {
            var tokenEntite = await _db.AchatEmailTokens
                .Where(t => t.Provider == "gmail")
                .OrderByDescending(t => t.ConfigureeLe)
                .FirstOrDefaultAsync();

            if (tokenEntite == null) return new();

            var credential = await ObtenirCredentialAsync(tokenEntite);
            var service    = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName       = "SKYRA ERP"
            });

            // Cherche les emails non lus dont le sujet contient CONFIRMER ou REFUSER
            var listReq = service.Users.Messages.List("me");
            listReq.Q          = "subject:CONFIRMER OR subject:REFUSER is:unread";
            listReq.MaxResults = 20;

            var listResp = await listReq.ExecuteAsync();
            var resultats = new List<(string, bool, string)>();

            if (listResp.Messages == null) return resultats;

            foreach (var msg in listResp.Messages)
            {
                var fullMsg = await service.Users.Messages.Get("me", msg.Id).ExecuteAsync();
                var sujet   = fullMsg.Payload?.Headers?
                    .FirstOrDefault(h => h.Name == "Subject")?.Value ?? "";

                var match = RegexReponse.Match(sujet);
                if (!match.Success) continue;

                string numeroBc = match.Groups[1].Value.ToUpper();
                bool   confirme = match.Groups[2].Value.ToUpper() == "CONFIRMER";

                resultats.Add((numeroBc, confirme, msg.Id));
            }

            return resultats;
        }

        // ── Marque un email comme lu (supprime le label UNREAD) ───────────────
        public async Task MarquerLuAsync(string messageId)
        {
            var tokenEntite = await _db.AchatEmailTokens
                .Where(t => t.Provider == "gmail")
                .OrderByDescending(t => t.ConfigureeLe)
                .FirstOrDefaultAsync();

            if (tokenEntite == null) return;

            var credential = await ObtenirCredentialAsync(tokenEntite);
            var service    = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName       = "SKYRA ERP"
            });

            var modifyReq = new ModifyMessageRequest
            {
                RemoveLabelIds = new List<string> { "UNREAD" }
            };

            await service.Users.Messages.Modify(modifyReq, "me", messageId).ExecuteAsync();
        }

        // ── Helpers privés ────────────────────────────────────────────────────

        private GoogleAuthorizationCodeFlow CreerFlow(string redirectUri)
        {
            return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId     = ClientId,
                    ClientSecret = ClientSecret
                },
                Scopes = new[] { Scope }
            });
        }

        private async Task<UserCredential> ObtenirCredentialAsync(AchatEmailToken tokenEntite)
        {
            var flow      = CreerFlow("");
            var tokenResp = new TokenResponse
            {
                AccessToken      = tokenEntite.AccessToken,
                RefreshToken     = tokenEntite.RefreshToken,
                ExpiresInSeconds = (long)(tokenEntite.ExpiresAt - DateTime.UtcNow).TotalSeconds
            };

            var credential = new UserCredential(flow, "user", tokenResp);

            if (tokenEntite.ExpiresAt <= DateTime.UtcNow.AddMinutes(5))
            {
                await credential.RefreshTokenAsync(CancellationToken.None);

                tokenEntite.AccessToken = credential.Token.AccessToken;
                tokenEntite.ExpiresAt   = DateTime.UtcNow.AddSeconds(
                    credential.Token.ExpiresInSeconds ?? 3600);
                await _db.SaveChangesAsync();
            }

            return credential;
        }

        private async Task<string> ObtenirEmailAdresseAsync(string accessToken)
        {
            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var json = await http.GetStringAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("email").GetString() ?? "";
        }

        // ── Template email ────────────────────────────────────────────────────
        /// <summary>
        /// Génère le corps HTML du BC. Les boutons Confirmer/Refuser sont des liens
        /// mailto: — le fournisseur répond par email, aucune URL publique requise.
        /// </summary>
        private string GenererCorpsEmail(AchatBonCommande bc, string emailERP)
        {
            string dateLivraison = bc.DateLivraisonSouhaitee.HasValue
                ? bc.DateLivraisonSouhaitee.Value.ToString("dd/MM/yyyy")
                : "À définir";

            // Encode le sujet pour l'URL mailto
            string sujetConfirmer = Uri.EscapeDataString($"{bc.Numero} CONFIRMER");
            string sujetRefuser   = Uri.EscapeDataString($"{bc.Numero} REFUSER");
            string mailtoConfirmer = $"mailto:{emailERP}?subject={sujetConfirmer}";
            string mailtoRefuser   = $"mailto:{emailERP}?subject={sujetRefuser}";

            var lignesHtml = new StringBuilder();
            if (bc.Lignes?.Any() == true)
            {
                lignesHtml.Append(@"
      <table style=""width:100%;border-collapse:collapse;margin:24px 0;font-size:13px;"">
        <thead>
          <tr style=""background:rgba(123,94,255,0.2);"">
            <th style=""padding:10px 12px;text-align:left;color:#A4A7C8;font-weight:600;"">Composant</th>
            <th style=""padding:10px 12px;text-align:right;color:#A4A7C8;font-weight:600;"">Quantité</th>
            <th style=""padding:10px 12px;text-align:right;color:#A4A7C8;font-weight:600;"">Prix HT</th>
            <th style=""padding:10px 12px;text-align:right;color:#A4A7C8;font-weight:600;"">Total HT</th>
          </tr>
        </thead><tbody>");
                foreach (var l in bc.Lignes.Where(l => !l.EstExclue))
                {
                    lignesHtml.Append($@"
          <tr style=""border-bottom:1px solid rgba(255,255,255,0.06);"">
            <td style=""padding:10px 12px;color:#e0e0e0;"">{l.Produit?.Nom ?? "—"}{(l.EstSousTraitance ? " <span style='font-size:11px;color:#fbbf24;'>(sous-traitance)</span>" : "")}</td>
            <td style=""padding:10px 12px;text-align:right;color:#e0e0e0;"">{l.Quantite:N3}</td>
            <td style=""padding:10px 12px;text-align:right;color:#e0e0e0;"">{l.PrixUnitaireHT:N2} DZD</td>
            <td style=""padding:10px 12px;text-align:right;font-weight:700;color:#9C8CFF;"">{l.TotalHT:N2} DZD</td>
          </tr>");
                }
                lignesHtml.Append("</tbody></table>");
            }

            string notesHtml = string.IsNullOrWhiteSpace(bc.Notes) ? "" : $@"
      <div style=""background:rgba(255,255,255,0.04);border-left:3px solid #7B5EFF;border-radius:4px;padding:12px 16px;margin:16px 0;"">
        <p style=""margin:0;font-size:12px;color:#A4A7C8;font-weight:600;text-transform:uppercase;letter-spacing:.05em;"">Notes</p>
        <p style=""margin:6px 0 0;font-size:13px;color:#e0e0e0;"">{System.Web.HttpUtility.HtmlEncode(bc.Notes)}</p>
      </div>";

            return $@"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"">
  <style>
    body{{font-family:'Segoe UI',Arial,sans-serif;background:#0d0f1a;margin:0;padding:20px;}}
    .wrap{{max-width:620px;margin:0 auto;background:#13152b;border-radius:20px;overflow:hidden;border:1px solid rgba(123,94,255,0.2);}}
    .hd{{background:linear-gradient(135deg,#7B5EFF,#5B3EDF);padding:36px 32px;text-align:center;}}
    .hd h1{{color:#fff;margin:0;font-size:26px;font-weight:800;}}
    .hd p{{color:rgba(255,255,255,.75);margin:6px 0 0;font-size:14px;}}
    .bd{{padding:32px;color:#e0e0e0;}}
    .row{{display:flex;justify-content:space-between;align-items:center;padding:11px 0;border-bottom:1px solid rgba(255,255,255,.07);}}
    .lbl{{color:#A4A7C8;font-size:13px;}}.val{{color:#fff;font-weight:600;font-size:13px;}}
    .tot{{background:rgba(123,94,255,.12);border:1px solid rgba(123,94,255,.25);border-radius:14px;padding:20px;margin:24px 0;text-align:center;}}
    .tot .m{{font-size:30px;font-weight:800;color:#9C8CFF;}}
    .tot .s{{color:#A4A7C8;font-size:13px;margin-top:6px;}}
    .btns{{text-align:center;margin:28px 0;}}
    .btn{{display:inline-block;text-decoration:none;padding:15px 30px;border-radius:999px;font-weight:700;font-size:15px;margin:6px;}}
    .ok{{background:linear-gradient(135deg,#22c55e,#16a34a);color:#fff;}}
    .ko{{background:transparent;color:#ef4444;border:1px solid #ef4444;padding:14px 29px;}}
    .hint{{background:rgba(123,94,255,.08);border:1px solid rgba(123,94,255,.2);border-radius:12px;padding:14px 18px;margin:20px 0;font-size:12px;color:#A4A7C8;line-height:1.6;text-align:center;}}
    .ft{{padding:20px 32px;text-align:center;color:#7F83A5;font-size:12px;border-top:1px solid rgba(255,255,255,.06);}}
  </style>
</head>
<body>
<div class=""wrap"">
  <div class=""hd""><h1>SKYRA ERP</h1><p>Bon de Commande {bc.Numero}</p></div>
  <div class=""bd"">
    <p style=""margin:0 0 20px;font-size:14px;color:#A4A7C8;"">Bonjour,<br><br>
    Veuillez trouver ci-dessous notre bon de commande. Merci de confirmer votre acceptation ou votre refus via les boutons ci-dessous.</p>
    <div class=""row""><span class=""lbl"">Référence BC</span><span class=""val"">{bc.Numero}</span></div>
    <div class=""row""><span class=""lbl"">Date de commande</span><span class=""val"">{bc.DateCommande:dd/MM/yyyy}</span></div>
    <div class=""row""><span class=""lbl"">Livraison souhaitée</span><span class=""val"">{dateLivraison}</span></div>
    {lignesHtml}
    {notesHtml}
    <div class=""tot"">
      <div class=""m"">{bc.TotalTTC:N2} DZD TTC</div>
      <div class=""s"">Total HT : {bc.TotalHT:N2} DZD &nbsp;·&nbsp; TVA 19% : {bc.MontantTVA:N2} DZD</div>
    </div>
    <div class=""btns"">
      <a href=""{mailtoConfirmer}"" class=""btn ok"">✓&nbsp; Confirmer la commande</a>
      <a href=""{mailtoRefuser}"" class=""btn ko"">✗&nbsp; Refuser</a>
    </div>
    <div class=""hint"">
      Cliquez sur un bouton — votre messagerie s'ouvrira avec le sujet pré-rempli.<br>
      Envoyez simplement l'email. Votre réponse sera enregistrée automatiquement.
    </div>
  </div>
  <div class=""ft"">Envoyé automatiquement par <strong style=""color:#9C8CFF;"">SKYRA ERP</strong>.</div>
</div>
</body></html>";
        }
    }
}
