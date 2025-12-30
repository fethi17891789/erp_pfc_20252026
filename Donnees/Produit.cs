using System;
using System.ComponentModel.DataAnnotations;

namespace Donnees
{
    public class Produit
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Nom { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Reference { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CodeBarres { get; set; } = string.Empty;

        public string Type { get; set; } = "Bien";

        public decimal PrixVente { get; set; } = 0m;

        public decimal Cout { get; set; } = 0m;

        public bool DisponibleVente { get; set; } = true;

        public bool SuiviInventaire { get; set; } = true;

        public string? Image { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime DateCreation { get; set; }  // ← PAS DE VALEUR PAR DÉFAUT
    }
}
