// Fichier : Donnees/MRPPlan.cs
using System;
using System.Collections.Generic;

namespace Donnees
{
    public class MRPPlan
    {
        public int Id { get; set; }

        public string Reference { get; set; } = string.Empty;

        public DateTime DateCreation { get; set; }

        public DateTime DateDebutHorizon { get; set; }

        public DateTime DateFinHorizon { get; set; }

        public int HorizonJours { get; set; }

        public string Statut { get; set; } = "Brouillon"; // Brouillon / Sauvegardee / Annulee...

        public string? TypePlan { get; set; } // Simulation / Officiel ...

        public string? Commentaire { get; set; }

        public ICollection<MRPPlanLigne> Lignes { get; set; } = new List<MRPPlanLigne>();
    }
}
