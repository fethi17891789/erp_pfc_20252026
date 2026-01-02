// Fichier : Donnees/Bom.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Donnees
{
    /// <summary>
    /// En‑tête de nomenclature (BOM) pour un produit fini.
    /// </summary>
    [Table("Boms")]
    public class Bom
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Produit principal (fini) auquel la nomenclature est rattachée.
        /// </summary>
        [Required]
        public int ProduitId { get; set; }

        /// <summary>
        /// Navigation vers le produit fini.
        /// </summary>
        public Produit? Produit { get; set; }

        /// <summary>
        /// Lignes de composants de cette nomenclature.
        /// </summary>
        public List<BomLigne> Lignes { get; set; } = new();
    }

    /// <summary>
    /// Ligne de nomenclature : un composant + quantité pour un produit fini.
    /// </summary>
    [Table("BomLignes")]
    public class BomLigne
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Référence vers la BOM parente.
        /// </summary>
        [Required]
        public int BomId { get; set; }

        public Bom? Bom { get; set; }

        /// <summary>
        /// Produit composant.
        /// </summary>
        [Required]
        public int ComposantProduitId { get; set; }

        public Produit? ComposantProduit { get; set; }

        /// <summary>
        /// Quantité de ce composant dans la BOM.
        /// </summary>
        [Range(0.0001, double.MaxValue)]
        public decimal Quantite { get; set; }

        /// <summary>
        /// Prix unitaire du composant au moment de la BOM (copie pour l’historique).
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrixUnitaire { get; set; }
    }
}
