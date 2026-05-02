// Fichier : Donnees/Achats/AchatFactureFournisseur.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Donnees.Achats
{
    public static class StatutFactureFournisseur
    {
        public const string Recue        = "Recue";
        public const string Verifiee     = "Verifiee";
        public const string Comptabilisee = "Comptabilisee";
    }

    /// <summary>
    /// Facture reçue d'un fournisseur, rapprochée avec le BC et le BR.
    /// TVA fixe à 19%. Alerte si écart > 2% avec le BC.
    /// Numérotation automatique : FAC-AAAA-NNN
    /// </summary>
    public class AchatFactureFournisseur
    {
        public int Id { get; set; }

        /// <summary>Numéro automatique SKYRA : FAC-2025-001</summary>
        [Required, MaxLength(20)]
        public string Numero { get; set; } = string.Empty;

        /// <summary>Numéro de facture du fournisseur (saisi manuellement depuis le document papier).</summary>
        [MaxLength(100)]
        public string? NumeroFournisseur { get; set; }

        public int BonCommandeId { get; set; }
        public AchatBonCommande? BonCommande { get; set; }

        public int? BonReceptionId { get; set; }
        public AchatBonReception? BonReception { get; set; }

        public DateTime DateFacture { get; set; } = DateTime.UtcNow;

        /// <summary>Montant HT tel qu'indiqué sur la facture fournisseur.</summary>
        public decimal MontantHT { get; set; } = 0m;

        /// <summary>TVA calculée automatiquement à 19%.</summary>
        public decimal MontantTVA { get; set; } = 0m;

        /// <summary>Total TTC = MontantHT + MontantTVA.</summary>
        public decimal MontantTTC { get; set; } = 0m;

        /// <summary>
        /// Vrai si l'écart entre MontantHT et le TotalHT du BC dépasse 2%.
        /// Déclenche une alerte visuelle non bloquante.
        /// </summary>
        public bool AlerteEcartPrix { get; set; } = false;
        public decimal EcartPourcentage { get; set; } = 0m;

        [MaxLength(30)]
        public string Statut { get; set; } = StatutFactureFournisseur.Recue;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public int? CreeParUserId { get; set; }
    }
}
