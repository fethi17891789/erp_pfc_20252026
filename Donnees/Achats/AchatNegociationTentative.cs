// Fichier : Donnees/Achats/AchatNegociationTentative.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Donnees.Achats
{
    public static class StatutTentative
    {
        public const string EnAttente        = "EnAttente";
        public const string Acceptee         = "Acceptee";
        public const string ContreProposition = "ContreProposition";
        public const string Expiree          = "Expiree";
        public const string Refusee          = "Refusee";
    }

    /// <summary>
    /// Représente un envoi du BC au fournisseur (une tentative de négociation).
    /// Chaque renvoi crée une nouvelle tentative ; la précédente passe à Expiree.
    /// </summary>
    public class AchatNegociationTentative
    {
        public int Id { get; set; }

        public int BonCommandeId { get; set; }
        public AchatBonCommande? BonCommande { get; set; }

        /// <summary>Numéro ordinal : 1, 2, 3…</summary>
        public int Numero { get; set; } = 1;

        /// <summary>Token unique envoyé au fournisseur par email.</summary>
        [MaxLength(100)]
        public string? Token { get; set; }

        public DateTime DateEnvoi { get; set; } = DateTime.UtcNow;

        [MaxLength(30)]
        public string Statut { get; set; } = StatutTentative.EnAttente;

        /// <summary>Message laissé par le fournisseur lors de sa réponse.</summary>
        [MaxLength(1000)]
        public string? MessageFournisseur { get; set; }

        public DateTime? DateReponse { get; set; }

        // Navigation
        public List<AchatNegociationLigne> Lignes { get; set; } = new();
    }
}
