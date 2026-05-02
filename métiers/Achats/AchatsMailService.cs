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
            byte[]? pdfBlob = null)
        {
            var smtpHost     = _config["SmtpAchats:Host"] ?? "smtp.gmail.com";
            var smtpPort     = int.Parse(_config["SmtpAchats:Port"] ?? "587");
            var smtpUser     = _config["SmtpAchats:User"] ?? "";
            var smtpPassword = _config["SmtpAchats:Password"] ?? "";
            var smtpFrom     = _config["SmtpAchats:From"] ?? smtpUser;

            if (string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPassword))
                throw new InvalidOperationException("SMTP non configuré. Veuillez configurer l'email dans les Paramètres.");

            string lienConfirmation = $"{baseUrl}/Achats/Confirmer?token={bc.TokenConfirmation}";

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

        private string GenererCorpsEmail(AchatBonCommande bc, string lienConfirmation)
        {
            string dateLivraison = bc.DateLivraisonSouhaitee.HasValue
                ? bc.DateLivraisonSouhaitee.Value.ToString("dd/MM/yyyy")
                : "À définir";

            return $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; background: #f5f5f5; margin: 0; padding: 20px; }}
    .container {{ max-width: 600px; margin: 0 auto; background: #1a1a2e; border-radius: 16px; overflow: hidden; }}
    .header {{ background: linear-gradient(135deg, #7B5EFF, #5B3EDF); padding: 32px; text-align: center; }}
    .header h1 {{ color: white; margin: 0; font-size: 24px; }}
    .header p {{ color: rgba(255,255,255,0.7); margin: 8px 0 0; }}
    .body {{ padding: 32px; color: #e0e0e0; }}
    .info-row {{ display: flex; justify-content: space-between; padding: 12px 0; border-bottom: 1px solid rgba(255,255,255,0.08); }}
    .info-label {{ color: #A4A7C8; font-size: 13px; }}
    .info-value {{ color: white; font-weight: 600; }}
    .total {{ background: rgba(123,94,255,0.15); border-radius: 12px; padding: 16px; margin: 24px 0; text-align: center; }}
    .total .montant {{ font-size: 28px; font-weight: 800; color: #7B5EFF; }}
    .total .label {{ color: #A4A7C8; font-size: 13px; margin-top: 4px; }}
    .btn-container {{ text-align: center; margin: 32px 0; }}
    .btn {{ display: inline-block; text-decoration: none; padding: 16px 32px; border-radius: 999px; font-weight: 700; font-size: 16px; margin: 8px; }}
    .btn-confirmer {{ background: linear-gradient(135deg, #22c55e, #16a34a); color: white; }}
    .btn-refuser {{ background: rgba(239,68,68,0.2); color: #ef4444; border: 1px solid #ef4444; }}
    .footer {{ padding: 24px; text-align: center; color: #7F83A5; font-size: 12px; }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>SKYRA ERP</h1>
      <p>Bon de Commande {bc.Numero}</p>
    </div>
    <div class=""body"">
      <p>Bonjour,</p>
      <p>Veuillez trouver ci-joint notre bon de commande. Merci de confirmer votre acceptation ou votre refus en cliquant sur l'un des boutons ci-dessous.</p>

      <div class=""info-row"">
        <span class=""info-label"">Référence BC</span>
        <span class=""info-value"">{bc.Numero}</span>
      </div>
      <div class=""info-row"">
        <span class=""info-label"">Date de commande</span>
        <span class=""info-value"">{bc.DateCommande:dd/MM/yyyy}</span>
      </div>
      <div class=""info-row"">
        <span class=""info-label"">Date de livraison souhaitée</span>
        <span class=""info-value"">{dateLivraison}</span>
      </div>

      <div class=""total"">
        <div class=""montant"">{bc.TotalTTC:N2} DZD TTC</div>
        <div class=""label"">Total HT : {bc.TotalHT:N2} DZD — TVA 19% : {bc.MontantTVA:N2} DZD</div>
      </div>

      <div class=""btn-container"">
        <a href=""{lienConfirmation}&reponse=confirmer"" class=""btn btn-confirmer"">✓ Confirmer la commande</a>
        <a href=""{lienConfirmation}&reponse=refuser"" class=""btn btn-refuser"">✗ Refuser</a>
      </div>

      <p style=""text-align:center; color:#A4A7C8; font-size:13px;"">
        Ou consultez la commande complète et laissez un message en cliquant ici :<br>
        <a href=""{lienConfirmation}"" style=""color:#7B5EFF;"">{lienConfirmation}</a>
      </p>
    </div>
    <div class=""footer"">
      Cet email a été envoyé automatiquement par SKYRA ERP.<br>
      Ce lien de confirmation est à usage unique.
    </div>
  </div>
</body>
</html>";
        }
    }
}
