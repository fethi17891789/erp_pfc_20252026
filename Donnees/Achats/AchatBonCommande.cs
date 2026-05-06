// Fichier : Donnees/Achats/AchatBonCommande.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Donnees.Achats
{
    /// <summary>
    /// Statuts possibles d'un Bon de Commande fournisseur.
    /// </summary>
    public static class StatutBonCommande
    {
        public const string Brouillon          = "Brouillon";
        public const string Envoye             = "Envoye";
        public const string Confirme           = "Confirme";
        public const string PartiellemtRecu    = "PartiellemtRecu";
        public const string Recu               = "Recu";
        public const string Facture            = "Facture";
        public const string Refuse             = "Refuse";
        public const string EnNegociation     = "EnNegociation";
        public const string Annule            = "Annule";
    }

    /// <summary>
    /// Bon de Commande fournisseur (document officiel d'achat).
    /// Numérotation automatique : BC-AAAA-NNN
    /// </summary>
    public class AchatBonCommande
    {
        public int Id { get; set; }

        /// <summary>Numéro automatique formaté : BC-2025-001</summary>
        [Required, MaxLength(20)]
        public string Numero { get; set; } = string.Empty;

        /// <summary>Fournisseur (Contact de type Fournisseur uniquement).</summary>
        public int FournisseurId { get; set; }
        public Contact? Fournisseur { get; set; }

        public DateTime DateCommande { get; set; } = DateTime.UtcNow;
        public DateTime? DateLivraisonSouhaitee { get; set; }

        /// <summary>Statut du BC (voir StatutBonCommande).</summary>
        [MaxLength(30)]
        public string Statut { get; set; } = StatutBonCommande.Brouillon;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        /// <summary>Token unique pour la page de confirmation publique fournisseur (sans login).</summary>
        [MaxLength(100)]
        public string? TokenConfirmation { get; set; }

        /// <summary>PDF du BC généré et stocké en base (BYTEA).</summary>
        public byte[]? PdfBlob { get; set; }

        public DateTime? DateEnvoiMail { get; set; }

        /// <summary>Message laissé par le fournisseur lors de sa réponse (via interface email).</summary>
        [MaxLength(500)]
        public string? RepondeurMessage { get; set; }

        public DateTime? DateReponse { get; set; }

        /// <summary>Date de livraison proposée par le fournisseur (si différente de souhaitée).</summary>
        public DateTime? DateLivraisonProposee { get; set; }

        // === Totaux calculés ===
        public decimal TotalHT { get; set; } = 0m;
        public decimal MontantTVA { get; set; } = 0m;
        public decimal TotalTTC { get; set; } = 0m;

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public int? CreeParUserId { get; set; }

        // Navigation
        public List<AchatBonCommandeLigne> Lignes { get; set; } = new();
        public List<AchatProforma> Proformas { get; set; } = new();
        public List<AchatBonReception> BonsReception { get; set; } = new();
        public List<AchatNegociationTentative> Tentatives { get; set; } = new();
    }
}
