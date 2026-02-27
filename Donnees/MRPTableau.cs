// Fichier : Donnees/MRPTableau.cs
using System;

namespace Donnees
{
    /// <summary>
    /// Détail périodique d'une ligne MRP (tableau MRP : BB, SP, BN, FIN, DEB, DEL par période).
    /// </summary>
    public class MRPTableau
    {
        public int Id { get; set; }

        // Ligne agrégée à laquelle ce détail appartient
        public int MRPPlanLigneId { get; set; }
        public MRPPlanLigne MRPPlanLigne { get; set; } = null!;

        // Numéro de période dans l'horizon (0,1,2,...) et date correspondante
        public int NumeroPeriode { get; set; }
        public DateTime DatePeriode { get; set; }

        // Valeurs MRP pour cette période
        public decimal BesoinBrut { get; set; }          // BB
        public decimal StockPrevisionnel { get; set; }   // SP
        public decimal BesoinNet { get; set; }           // BN
        public decimal FinOrdre { get; set; }            // FIN (réceptions)
        public decimal DebutOrdre { get; set; }          // DEB (lancements)
        public int DelaiJours { get; set; }              // DEL
    }
}
