using System;
using System.ComponentModel.DataAnnotations;

namespace Donnees.Logistique
{
    public class Capteur
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string IdentifiantUnique { get; set; } // UUID du téléphone ou ID de l'appareil

        public int? VehiculeId { get; set; } // Le véhicule auquel il est associé

        public string Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    }
}
