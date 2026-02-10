// Fichier : Donnees/Produit.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Donnees
{
    public enum TypeTechniqueProduit
    {
        MatierePremiere = 0,
        SemiFini = 1,
        Fini = 2,
        SemiFiniEtFini = 3
    }

    public class Produit
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Nom { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Reference { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CodeBarres { get; set; } = string.Empty;

        // Type commercial existant (Bien / Service)
        public string Type { get; set; } = "Bien";

        public decimal PrixVente { get; set; } = 0m;

        // Ancien champ Cout : on continue de l’utiliser comme "coût d’achat de base"
        public decimal Cout { get; set; } = 0m;

        // Quantité disponible
        public decimal QuantiteDisponible { get; set; } = 0m;

        public bool DisponibleVente { get; set; } = true;

        public bool SuiviInventaire { get; set; } = true;

        public string? Image { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime DateCreation { get; set; }

        // ==================== COÛTS TECHNIQUES ====================

        // Type technique : MP / Semi-fini / Fini / Semi-fini + Fini
        public TypeTechniqueProduit TypeTechnique { get; set; } = TypeTechniqueProduit.MatierePremiere;

        // Coût d'achat (pour MP ou produits achetés)
        public decimal CoutAchat { get; set; } = 0m;

        // Autres charges (assemblage, énergie, main-d'œuvre...)
        public decimal CoutAutresCharges { get; set; } = 0m;

        // Coût de nomenclature (somme des composants)
        public decimal CoutBom { get; set; } = 0m;

        // Coût total = CoutBom + CoutAutresCharges (ou CoutAchat + charges pour MP)
        public decimal CoutTotal { get; set; } = 0m;
    }
}
