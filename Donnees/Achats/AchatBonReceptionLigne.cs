// Fichier : Donnees/Achats/AchatBonReceptionLigne.cs

namespace Donnees.Achats
{
    public static class EtatReceptionLigne
    {
        public const string Conforme   = "Conforme";
        public const string Endommage  = "Endommage";
        public const string Manquant   = "Manquant";
    }

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

        /// <summary>Quantité effectivement reçue (saisie lors de la réception).</summary>
        public decimal QuantiteRecue { get; set; } = 0m;

        /// <summary>État de la réception (voir EtatReceptionLigne).</summary>
        public string Etat { get; set; } = EtatReceptionLigne.Conforme;
    }
}
