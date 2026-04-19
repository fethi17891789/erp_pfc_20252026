using System;
using System.ComponentModel.DataAnnotations;

namespace Donnees.Logistique
{
    public class Vehicule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nom { get; set; }

        [StringLength(50)]
        public string Matricule { get; set; }

        [Required]
        [StringLength(50)]
        public string TypeTransport { get; set; } // Camion, Fourgonnette, etc.

        public string Statut { get; set; } = "Disponible"; // Disponible, En Trajet, Maintenance

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public DateTime? DerniereMiseAJour { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;

        // ─── Champs RSE / Empreinte Carbone ─────────────────────────────────
        [StringLength(100)]
        public string? Marque { get; set; }

        [StringLength(100)]
        public string? Modele { get; set; }

        public int? Annee { get; set; }

        /// <summary>Essence, Diesel, Hybride, Électrique, GNV</summary>
        [StringLength(50)]
        public string? TypeCarburant { get; set; }

        /// <summary>Émission CO2 en g/km (estimée formule ADEME + IA Gemini)</summary>
        public double? EmissionCO2ParKm { get; set; }
    }
}
