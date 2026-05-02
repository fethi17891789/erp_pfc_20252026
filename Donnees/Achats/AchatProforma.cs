// Fichier : Donnees/Achats/AchatProforma.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Donnees.Achats
{
    /// <summary>
    /// Proforma reçu du fournisseur en réponse à un BC.
    /// Stocké dans SKYRA pour référence, pas généré par SKYRA.
    /// </summary>
    public class AchatProforma
    {
        public int Id { get; set; }

        public int BonCommandeId { get; set; }
        public AchatBonCommande? BonCommande { get; set; }

        public DateTime DateReception { get; set; } = DateTime.UtcNow;

        /// <summary>Montant HT tel qu'indiqué dans le proforma fournisseur.</summary>
        public decimal MontantHT { get; set; } = 0m;

        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>Fichier PDF du proforma uploadé (optionnel).</summary>
        [MaxLength(255)]
        public string? FichierNom { get; set; }
        public byte[]? FichierBlob { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public int? CreeParUserId { get; set; }
    }
}
