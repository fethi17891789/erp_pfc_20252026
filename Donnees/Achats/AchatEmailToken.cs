// Fichier : Donnees/Achats/AchatEmailToken.cs
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Donnees.Achats
{
    /// <summary>
    /// Stocke les tokens OAuth2 pour l'envoi d'email (Gmail, Outlook…).
    /// Une seule ligne active à la fois (le dernier token configuré).
    /// </summary>
    [Table("AchatEmailTokens")]
    public class AchatEmailToken
    {
        public int      Id            { get; set; }
        public string   Provider      { get; set; } = "gmail";   // gmail | outlook
        public string   EmailAdresse  { get; set; } = string.Empty;
        public string   AccessToken   { get; set; } = string.Empty;
        public string   RefreshToken  { get; set; } = string.Empty;
        public DateTime ExpiresAt     { get; set; }
        public DateTime ConfigureeLe  { get; set; } = DateTime.UtcNow;
    }
}
