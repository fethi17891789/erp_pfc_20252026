using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages
{
    public class MRPDetailModel : PageModel
    {
        public class PlanificationVm
        {
            public int Id { get; set; }
            public string Reference { get; set; } = string.Empty;
            public int HorizonJours { get; set; }
            public DateTime DateDebut { get; set; }
            public DateTime DateFin { get; set; }
            public string TypeOrdre { get; set; } = "OF"; // OF / OA (logique future)
            public string Statut { get; set; } = "Brouillon";
        }

        public class LigneMrpVm
        {
            public string CodeArticle { get; set; } = string.Empty;
            public string LibelleArticle { get; set; } = string.Empty;

            // Type de produit selon ta fiche produit : Fini / Semi-fini (on pourra aligner sur ta enum plus tard)
            public string TypeProduit { get; set; } = "Fini";

            // Unité (ex : PCS, KG, etc.) cohérente avec ton modèle produit
            public string Unite { get; set; } = "PCS";

            public DateTime DateBesoin { get; set; }
            public decimal QuantiteBesoin { get; set; }

            // Stock disponible au moment du calcul
            public decimal StockDisponible { get; set; }

            // Quantité à lancer = besoin - dispo (simplifié pour la démo)
            public decimal QuantiteALancer { get; set; }
        }

        public PlanificationVm? Planification { get; set; }
        public List<LigneMrpVm> Lignes { get; set; } = new List<LigneMrpVm>();

        public void OnGet(int id, int? horizonJours)
        {
            // Plus tard : remplacement par un vrai chargement BDD (planif + lignes MRP)
            if (id == 1)
            {
                Planification = new PlanificationVm
                {
                    Id = 1,
                    Reference = "MRP-0001",
                    HorizonJours = 30,
                    DateDebut = new DateTime(2026, 2, 13),
                    DateFin = new DateTime(2026, 3, 14),
                    TypeOrdre = "OF",
                    Statut = "Brouillon"
                };

                Lignes = new List<LigneMrpVm>
                {
                    new LigneMrpVm
                    {
                        CodeArticle = "PROD-001",
                        LibelleArticle = "Produit fini A",
                        TypeProduit = "Fini",
                        Unite = "PCS",
                        DateBesoin = new DateTime(2026, 2, 25),
                        QuantiteBesoin = 50,
                        StockDisponible = 10,
                        QuantiteALancer = 40
                    },
                    new LigneMrpVm
                    {
                        CodeArticle = "SEMI-010",
                        LibelleArticle = "Sous-ensemble B",
                        TypeProduit = "Semi-fini",
                        Unite = "PCS",
                        DateBesoin = new DateTime(2026, 3, 2),
                        QuantiteBesoin = 30,
                        StockDisponible = 5,
                        QuantiteALancer = 25
                    },
                    new LigneMrpVm
                    {
                        CodeArticle = "PROD-003",
                        LibelleArticle = "Produit fini C",
                        TypeProduit = "Fini",
                        Unite = "PCS",
                        DateBesoin = new DateTime(2026, 3, 10),
                        QuantiteBesoin = 20,
                        StockDisponible = 0,
                        QuantiteALancer = 20
                    }
                };
            }
            else if (id == 2)
            {
                Planification = new PlanificationVm
                {
                    Id = 2,
                    Reference = "MRP-0002",
                    HorizonJours = 14,
                    DateDebut = new DateTime(2026, 2, 10),
                    DateFin = new DateTime(2026, 2, 24),
                    TypeOrdre = "OF",
                    Statut = "Sauvegardée"
                };

                Lignes = new List<LigneMrpVm>
                {
                    new LigneMrpVm
                    {
                        CodeArticle = "PROD-010",
                        LibelleArticle = "Produit fini X",
                        TypeProduit = "Fini",
                        Unite = "PCS",
                        DateBesoin = new DateTime(2026, 2, 18),
                        QuantiteBesoin = 30,
                        StockDisponible = 12,
                        QuantiteALancer = 18
                    },
                    new LigneMrpVm
                    {
                        CodeArticle = "SEMI-020",
                        LibelleArticle = "Semi-fini Y",
                        TypeProduit = "Semi-fini",
                        Unite = "PCS",
                        DateBesoin = new DateTime(2026, 2, 20),
                        QuantiteBesoin = 15,
                        StockDisponible = 0,
                        QuantiteALancer = 15
                    }
                };
            }
            else
            {
                Planification = new PlanificationVm
                {
                    Id = id,
                    Reference = "MRP-TEST",
                    HorizonJours = horizonJours ?? 7,
                    DateDebut = new DateTime(2026, 2, 5),
                    DateFin = new DateTime(2026, 2, 12),
                    TypeOrdre = "OF",
                    Statut = "Annulée (test)"
                };

                Lignes = new List<LigneMrpVm>
                {
                    new LigneMrpVm
                    {
                        CodeArticle = "PROD-TEST",
                        LibelleArticle = "Produit test MRP",
                        TypeProduit = "Fini",
                        Unite = "PCS",
                        DateBesoin = new DateTime(2026, 2, 8),
                        QuantiteBesoin = 10,
                        StockDisponible = 2,
                        QuantiteALancer = 8
                    }
                };
            }
        }
    }
}
