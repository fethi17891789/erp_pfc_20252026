// Fichier : Donnees/Achats/AchatBonCommandeLigne.cs

namespace Donnees.Achats
{
    /// <summary>
    /// Ligne d'un Bon de Commande (un composant commandé).
    /// </summary>
    public class AchatBonCommandeLigne
    {
        public int Id { get; set; }

        public int BonCommandeId { get; set; }
        public AchatBonCommande? BonCommande { get; set; }

        /// <summary>Composant commandé (lié à la table Produits).</summary>
        public int ProduitId { get; set; }
        public Produit? Produit { get; set; }

        /// <summary>
        /// Vrai si le composant n'est pas de type MatierePremiere.
        /// Dans ce cas, un popup d'avertissement sous-traitance a été confirmé.
        /// </summary>
        public bool EstSousTraitance { get; set; } = false;

        public decimal Quantite { get; set; } = 1m;
        public decimal PrixUnitaireHT { get; set; } = 0m;

        /// <summary>Calculé : Quantite × PrixUnitaireHT</summary>
        public decimal TotalHT { get; set; } = 0m;

        /// <summary>Vrai si l'acheteur a exclu cette ligne d'une renégociation (après refus fournisseur non réintégré).</summary>
        public bool EstExclue { get; set; } = false;
    }
}
