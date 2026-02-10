// Fichier : Donnees/ErpUser.cs
namespace Donnees
{
    public class ErpUser
    {
        public int Id { get; set; }

        // Identifiant de connexion
        public string Login { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        // Pour un vrai projet : stocker un hash, pas le mot de passe en clair
        public string Password { get; set; } = string.Empty;

        public string? LogoFileName { get; set; }

        // Nouveau : poste occupé dans l'entreprise
        public string Poste { get; set; } = string.Empty;

        // Nouveau : statut de connexion (en ligne / hors ligne)
        public bool IsOnline { get; set; } = false;
    }
}
