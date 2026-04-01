using System;
using System.ComponentModel.DataAnnotations;

namespace Donnees.Logistique
{
    public class Trajet
    {
        [Key]
        public int Id { get; set; }

        public int VehiculeId { get; set; }

        public int CapteurId { get; set; }

        public DateTime DateDebut { get; set; } = DateTime.UtcNow;

        public DateTime? DateFin { get; set; }

        public string? Origine { get; set; }

        public string? Destination { get; set; }

        public double DistanceParcourueKm { get; set; } = 0;

        public string Statut { get; set; } = "En Cours"; // En Cours, Termine, Annule

        // Stockage du tracé sous forme de JSON (points de passage)
        public string? TraceJson { get; set; } 
    }
}
