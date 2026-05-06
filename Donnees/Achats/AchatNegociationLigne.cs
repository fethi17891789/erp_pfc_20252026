// Fichier : Donnees/Achats/AchatNegociationLigne.cs

namespace Donnees.Achats
{
    /// <summary>
    /// Réponse du fournisseur pour une ligne précise du BC.
    /// Créée lors du traitement de la réponse fournisseur.
    /// </summary>
    public class AchatNegociationLigne
    {
        public int Id { get; set; }

        public int TentativeId { get; set; }
        public AchatNegociationTentative? Tentative { get; set; }

        /// <summary>Référence à la ligne du BC d'origine.</summary>
        public int BonCommandeLigneId { get; set; }
        public AchatBonCommandeLigne? BonCommandeLigne { get; set; }

        /// <summary>Prix proposé par le fournisseur. Null = accepte le prix du BC.</summary>
        public decimal? PrixProposeHT { get; set; }

        /// <summary>Vrai si le fournisseur refuse ce produit entièrement.</summary>
        public bool EstRefusee { get; set; } = false;
    }
}
