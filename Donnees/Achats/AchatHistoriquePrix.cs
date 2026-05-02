// Fichier : Donnees/Achats/AchatHistoriquePrix.cs
using System;

namespace Donnees.Achats
{
    /// <summary>
    /// Historique des prix d'achat par composant et par fournisseur.
    /// Enregistrement automatique à chaque validation d'un Bon de Réception.
    /// Utilisé pour le graphique 12 mois sur la fiche produit,
    /// le score de fiabilité fournisseur et le simulateur "Et si ?".
    /// </summary>
    public class AchatHistoriquePrix
    {
        public int Id { get; set; }

        public int ProduitId { get; set; }
        public Produit? Produit { get; set; }

        /// <summary>Fournisseur chez qui l'achat a été effectué.</summary>
        public int FournisseurId { get; set; }
        public Contact? Fournisseur { get; set; }

        /// <summary>Prix unitaire HT effectivement payé (issu du BC validé).</summary>
        public decimal PrixUnitaireHT { get; set; } = 0m;

        public decimal Quantite { get; set; } = 0m;

        /// <summary>Date de l'achat (date de validation du BR).</summary>
        public DateTime DateAchat { get; set; } = DateTime.UtcNow;

        /// <summary>BC à l'origine de cet enregistrement (pour traçabilité).</summary>
        public int BonCommandeId { get; set; }
    }
}
