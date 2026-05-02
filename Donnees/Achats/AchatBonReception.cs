// Fichier : Donnees/Achats/AchatBonReception.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Donnees.Achats
{
    public static class StatutBonReception
    {
        public const string EnCours = "EnCours";
        public const string Valide  = "Valide";   // Déclenche la mise à jour des quantités produits
    }

    /// <summary>
    /// Bon de Réception — enregistrement de la livraison physique d'un fournisseur.
    /// Numérotation automatique : BR-AAAA-NNN
    /// La validation met à jour la QuantiteDisponible de chaque produit reçu.
    /// </summary>
    public class AchatBonReception
    {
        public int Id { get; set; }

        /// <summary>Numéro automatique formaté : BR-2025-001</summary>
        [Required, MaxLength(20)]
        public string Numero { get; set; } = string.Empty;

        public int BonCommandeId { get; set; }
        public AchatBonCommande? BonCommande { get; set; }

        public DateTime DateReception { get; set; } = DateTime.UtcNow;

        [MaxLength(30)]
        public string Statut { get; set; } = StatutBonReception.EnCours;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public int? CreeParUserId { get; set; }

        // Navigation
        public List<AchatBonReceptionLigne> Lignes { get; set; } = new();
    }
}
