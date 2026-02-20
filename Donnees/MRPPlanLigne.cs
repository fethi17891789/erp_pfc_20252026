// Fichier : Donnees/MRPPlanLigne.cs
using System;

namespace Donnees
{
    public class MRPPlanLigne
    {
        public int Id { get; set; }

        public int MRPPlanId { get; set; }
        public MRPPlan MRPPlan { get; set; } = null!;

        public int ProduitId { get; set; }
        public Produit Produit { get; set; } = null!;

        // Fini / SemiFini (on aligne avec ton enum TypeTechniqueProduit)
        public string TypeProduit { get; set; } = "Fini";

        public DateTime DateBesoin { get; set; }

        public decimal QuantiteBesoin { get; set; }

        public decimal StockDisponible { get; set; }

        public decimal QuantiteALancer { get; set; }
    }
}
