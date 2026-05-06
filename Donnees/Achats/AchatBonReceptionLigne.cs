// Fichier : Donnees/Achats/AchatBonReceptionLigne.cs

namespace Donnees.Achats
{
    /// <summary>
    /// Ligne d'un Bon de Réception (quantités réellement reçues par composant).
    /// </summary>
    public class AchatBonReceptionLigne
    {
        public int Id { get; set; }

        public int BonReceptionId { get; set; }
        public AchatBonReception? BonReception { get; set; }

        public int ProduitId { get; set; }
        public Produit? Produit { get; set; }

        /// <summary>Quantité initialement commandée (issue du BC).</summary>
        public decimal QuantiteCommandee { get; set; } = 0m;

        /// <summary>Quantité effectivement reçue (y compris endommagée).</summary>
        public decimal QuantiteRecue { get; set; } = 0m;

        /// <summary>Quantité reçue endommagée.</summary>
        public decimal QuantiteEndommagee { get; set; } = 0m;
    }
}
