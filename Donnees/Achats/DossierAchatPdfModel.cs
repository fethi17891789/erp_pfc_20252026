using System;
using System.Collections.Generic;

namespace Donnees.Achats
{
    public class DossierAchatPdfModel
    {
        public string NumeroBc { get; set; } = "";
        public DateTime DateCommande { get; set; }
        public DateTime? DateLivraisonSouhaitee { get; set; }
        public string NomFournisseur { get; set; } = "";
        public string? EmailFournisseur { get; set; }

        public List<LigneBcPdf> LignesBc { get; set; } = new();

        public string? NumeroProforma { get; set; }
        public DateTime? DateProforma { get; set; }
        public string? MessageFournisseur { get; set; }
        public List<LigneProformaPdf> LignesProforma { get; set; } = new();
        public decimal ProformaTotalHT { get; set; }
        public decimal ProformaTVA { get; set; }
        public decimal ProformaTotalTTC { get; set; }

        public List<BonReceptionPdf> BonsReception { get; set; } = new();

        public string NumeroFacture { get; set; } = "";
        public DateTime DateFacture { get; set; }
        public decimal FactureHT { get; set; }
        public decimal FactureTVA { get; set; }
        public decimal FactureTTC { get; set; }
    }

    public class LigneBcPdf
    {
        public string NomProduit { get; set; } = "";
        public decimal Quantite { get; set; }
    }

    public class LigneProformaPdf
    {
        public string NomProduit { get; set; } = "";
        public decimal QuantiteDemandee { get; set; }
        public decimal QuantiteProposee { get; set; }
        public decimal PrixUnitaireHT { get; set; }
        public decimal TotalHT { get; set; }
    }

    public class BonReceptionPdf
    {
        public string Numero { get; set; } = "";
        public DateTime DateReception { get; set; }
        public List<LigneBrPdf> Lignes { get; set; } = new();
    }

    public class LigneBrPdf
    {
        public string NomProduit { get; set; } = "";
        public decimal QuantiteCommandee { get; set; }
        public decimal QuantiteRecue { get; set; }
        public decimal QuantiteEndommagee { get; set; }
        public decimal QuantiteConforme { get; set; }
    }
}
