// Fichier : Metier/Achats/AchatsMailService.cs
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Donnees.Achats;
using Microsoft.Extensions.Configuration;

namespace Metier.Achats
{
    /// <summary>
    /// Service d'envoi d'emails pour le module Achats.
    /// Utilise SMTP standard (Gmail ou autre).
    /// Configuration dans appsettings.json sous la clé "SmtpAchats".
    /// </summary>
    public class AchatsMailService
    {
        private readonly IConfiguration _config;

        public AchatsMailService(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Envoie le Bon de Commande au fournisseur par email.
        /// Inclut le lien de confirmation unique (token) et le PDF en pièce jointe.
        /// </summary>
        public async Task EnvoyerBonCommandeAsync(
            AchatBonCommande bc,
            string emailFournisseur,
            string baseUrl,
            byte[]? pdfBlob = null,
            string? token = null)
        {
            var smtpHost     = _config["SmtpAchats:Host"] ?? "smtp.gmail.com";
            var smtpPort     = int.Parse(_config["SmtpAchats:Port"] ?? "587");
            var smtpUser     = _config["SmtpAchats:User"] ?? "";
            var smtpPassword = _config["SmtpAchats:Password"] ?? "";
            var smtpFrom     = _config["SmtpAchats:From"] ?? smtpUser;

            if (string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPassword))
                throw new InvalidOperationException("SMTP non configuré. Veuillez configurer l'email dans les Paramètres.");

            string tokenEffectif    = token ?? bc.TokenConfirmation ?? "";
            string lienConfirmation = $"{baseUrl}/Achats/Confirmer?token={tokenEffectif}";

            string corps = GenererCorpsEmail(bc, lienConfirmation);

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPassword),
                EnableSsl   = true
            };

            using var message = new MailMessage(smtpFrom, emailFournisseur)
            {
                Subject    = $"Bon de Commande {bc.Numero} — SKYRA ERP",
                Body       = corps,
                IsBodyHtml = true
            };

            // Pièce jointe PDF si disponible
            if (pdfBlob != null && pdfBlob.Length > 0)
            {
                var attachment = new Attachment(
                    new System.IO.MemoryStream(pdfBlob),
                    $"{bc.Numero}.pdf",
                    "application/pdf");
                message.Attachments.Add(attachment);
            }

            await client.SendMailAsync(message);
        }

        /// <summary>
        /// Envoie un email d'acceptation de la contre-proposition au fournisseur.
        /// Contient juste les infos de prix HT/TTC, sans lien. Exclut les lignes refusées par le fournisseur.
        /// </summary>
        public async Task EnvoyerAcceptationContrePropositionAsync(
            AchatBonCommande bc,
            string emailFournisseur,
            AchatNegociationTentative? tentative = null)
        {
            var smtpHost     = _config["SmtpAchats:Host"] ?? "smtp.gmail.com";
            var smtpPort     = int.Parse(_config["SmtpAchats:Port"] ?? "587");
            var smtpUser     = _config["SmtpAchats:User"] ?? "";
            var smtpPassword = _config["SmtpAchats:Password"] ?? "";
            var smtpFrom     = _config["SmtpAchats:From"] ?? smtpUser;

            if (string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPassword))
                throw new InvalidOperationException("SMTP non configuré.");

            string corps = GenererCorpsEmailAcceptation(bc, tentative);

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPassword),
                EnableSsl   = true
            };

            using var message = new MailMessage(smtpFrom, emailFournisseur)
            {
                Subject    = $"Acceptation : Bon de Commande {bc.Numero} — SKYRA ERP",
                Body       = corps,
                IsBodyHtml = true
            };

            await client.SendMailAsync(message);
        }

        private string GenererCorpsEmail(AchatBonCommande bc, string lienConfirmation)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; background: #0d0f1a; margin: 0; padding: 20px; }}
    .wrap {{ max-width: 520px; margin: 0 auto; background: #13152b; border-radius: 20px; overflow: hidden; border: 1px solid rgba(123,94,255,0.2); }}
    .hd {{ background: linear-gradient(135deg, #7B5EFF, #5B3EDF); padding: 36px 32px; text-align: center; }}
    .hd h1 {{ color: #fff; margin: 0; font-size: 24px; font-weight: 800; }}
    .hd p {{ color: rgba(255,255,255,.75); margin: 6px 0 0; font-size: 14px; }}
    .bd {{ padding: 32px; text-align: center; color: #e0e0e0; }}
    .btn {{ display: inline-block; text-decoration: none; padding: 16px 40px; border-radius: 999px; font-weight: 700; font-size: 15px; background: linear-gradient(135deg, #7B5EFF, #5B3EDF); color: #fff; margin: 24px 0; }}
    .ft {{ padding: 20px 32px; text-align: center; color: #7F83A5; font-size: 12px; border-top: 1px solid rgba(255,255,255,.06); }}
  </style>
</head>
<body>
<div class=""wrap"">
  <div class=""hd""><h1>SKYRA ERP</h1><p>Bon de Commande {bc.Numero}</p></div>
  <div class=""bd"">
    <p style=""font-size:14px;color:#A4A7C8;line-height:1.7;margin:0 0 8px;"">
      Bonjour,<br><br>Vous avez reçu un bon de commande. Consultez les détails et répondez via le lien ci-dessous.
    </p>
    <a href=""{lienConfirmation}"" class=""btn"">Voir le bon de commande</a>
  </div>
  <div class=""ft"">Envoyé automatiquement par <strong style=""color:#9C8CFF;"">SKYRA ERP</strong>.</div>
</div>
</body></html>";
        }

        private string GenererCorpsEmailAcceptation(AchatBonCommande bc, AchatNegociationTentative? tentative = null)
        {
            // ── Tableau des lignes (exclut les refusées) ────────────────────────
            var lignesHtml = new System.Text.StringBuilder();
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
        </thead>
        <tbody>");
                foreach (var l in bc.Lignes.Where(l => !l.EstExclue))
                {
                    bool ligneRefusee = tentative?.Lignes?.Any(tl => tl.BonCommandeLigneId == l.Id && tl.EstRefusee) == true;
                    if (ligneRefusee) continue;

                    lignesHtml.Append($@"
          <tr style=""border-bottom:1px solid rgba(255,255,255,0.06);"">
            <td style=""padding:10px 12px;color:#e0e0e0;"">{l.Produit?.Nom ?? "—"}</td>
            <td style=""padding:10px 12px;text-align:right;color:#e0e0e0;"">{l.Quantite:N3}</td>
            <td style=""padding:10px 12px;text-align:right;color:#e0e0e0;"">{l.PrixUnitaireHT:N2} DZD</td>
            <td style=""padding:10px 12px;text-align:right;font-weight:700;color:#9C8CFF;"">{l.TotalHT:N2} DZD</td>
          </tr>");
                }
                lignesHtml.Append("</tbody></table>");
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; background: #0d0f1a; margin: 0; padding: 20px; }}
    .container {{ max-width: 620px; margin: 0 auto; background: #13152b; border-radius: 20px; overflow: hidden; border: 1px solid rgba(123,94,255,0.2); }}
    .header {{ background: linear-gradient(135deg, #22c55e, #16a34a); padding: 36px 32px; text-align: center; }}
    .header h1 {{ color: white; margin: 0; font-size: 26px; font-weight: 800; letter-spacing: -.5px; }}
    .header p {{ color: rgba(255,255,255,0.75); margin: 6px 0 0; font-size: 14px; }}
    .body {{ padding: 32px; color: #e0e0e0; }}
    .total-box {{ background: rgba(34,197,94,0.12); border: 1px solid rgba(34,197,94,0.25); border-radius: 14px; padding: 20px; margin: 24px 0; text-align: center; }}
    .total-box .montant {{ font-size: 30px; font-weight: 800; color: #22c55e; }}
    .total-box .label {{ color: #A4A7C8; font-size: 13px; margin-top: 6px; }}
    .footer {{ padding: 20px 32px; text-align: center; color: #7F83A5; font-size: 12px; border-top: 1px solid rgba(255,255,255,0.06); }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>✓ ACCEPTATION CONFIRMÉE</h1>
      <p>Bon de Commande {bc.Numero}</p>
    </div>
    <div class=""body"">
      <p style=""margin:0 0 20px;font-size:14px;color:#A4A7C8;"">Bonjour,<br><br>
      Nous confirmons l'acceptation de votre proposition pour le bon de commande ci-dessous.</p>

      {lignesHtml}

      <div class=""total-box"">
        <div class=""montant"">{bc.TotalTTC:N2} DZD TTC</div>
        <div class=""label"">Total HT : {bc.TotalHT:N2} DZD &nbsp;·&nbsp; TVA 19% : {bc.MontantTVA:N2} DZD</div>
      </div>

      <p style=""color:#A4A7C8;font-size:13px;margin:0;line-height:1.6;"">
        La commande est maintenant confirmée. Merci pour votre collaboration.
      </p>
    </div>
    <div class=""footer"">
      Cet email a été envoyé automatiquement par <strong style=""color:#22c55e;"">SKYRA ERP</strong>.
    </div>
  </div>
</body>
</html>";
        }
    }
}
