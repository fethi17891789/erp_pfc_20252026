// Fichier : Metier/Achats/AchatsPrixService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Donnees;
using Donnees.Achats;
using Microsoft.EntityFrameworkCore;

namespace Metier.Achats
{
    /// <summary>
    /// Service dédié aux fonctionnalités de prix avancées :
    /// - Graphique historique des prix (12 mois) par produit/fournisseur
    /// - Score de fiabilité fournisseur
    /// - Simulateur "Et si ?" (impact hausse de prix sur les BOMs)
    /// </summary>
    public class AchatsPrixService
    {
        private readonly ErpDbContext _db;

        public AchatsPrixService(ErpDbContext db)
        {
            _db = db;
        }

        // =====================================================================
        //  GRAPHIQUE HISTORIQUE DES PRIX
        // =====================================================================

        /// <summary>
        /// Retourne l'historique des prix sur les 12 derniers mois pour un produit,
        /// groupé par fournisseur. Format utilisable directement pour un graphique JS.
        /// </summary>
        public async Task<HistoriquePrixGraphe> GetHistoriqueGrapheAsync(int produitId)
        {
            var dateDebut = DateTime.UtcNow.AddMonths(-12);

            var historique = await _db.AchatHistoriquesPrix
                .Include(h => h.Fournisseur)
                .Where(h => h.ProduitId == produitId && h.DateAchat >= dateDebut)
                .OrderBy(h => h.DateAchat)
                .ToListAsync();

            var fournisseurs = historique
                .Select(h => new { h.FournisseurId, Nom = h.Fournisseur?.FullName ?? "Inconnu" })
                .Distinct()
                .ToList();

            var series = fournisseurs.Select(f => new SeriesPrix
            {
                FournisseurId  = f.FournisseurId,
                NomFournisseur = f.Nom,
                Points         = historique
                    .Where(h => h.FournisseurId == f.FournisseurId)
                    .Select(h => new PointPrix { Date = h.DateAchat, PrixHT = h.PrixUnitaireHT })
                    .ToList()
            }).ToList();

            // Prix BOM actuel du produit (CoutAchat en base)
            var produit = await _db.Produits.FindAsync(produitId);
            decimal prixBomActuel = produit?.CoutAchat ?? 0m;

            return new HistoriquePrixGraphe
            {
                ProduitId     = produitId,
                PrixBomActuel = prixBomActuel,
                Series        = series
            };
        }

        // =====================================================================
        //  SCORE FIABILITÉ FOURNISSEUR
        // =====================================================================

        /// <summary>
        /// Calcule le score de fiabilité d'un fournisseur (0–100, affiché en couleur).
        /// Basé sur : délais (40%), stabilité des prix (40%), qualité livraisons (20%).
        /// </summary>
        public async Task<ScoreFournisseur> CalculerScoreAsync(int fournisseurId)
        {
            var bcs = await _db.AchatBonCommandes
                .Include(b => b.BonsReception).ThenInclude(r => r.Lignes)
                .Where(b => b.FournisseurId == fournisseurId && b.Statut != StatutBonCommande.Brouillon)
                .ToListAsync();

            if (!bcs.Any())
                return new ScoreFournisseur { FournisseurId = fournisseurId, Score = -1, Niveau = "Inconnu" };

            // --- Délais (40%) ---
            var bcsAvecReception = bcs.Where(b =>
                b.DateLivraisonSouhaitee.HasValue &&
                b.BonsReception.Any(r => r.Statut == StatutBonReception.Valide)).ToList();

            double scoreDelais = 100;
            if (bcsAvecReception.Any())
            {
                int respectes = bcsAvecReception.Count(b =>
                {
                    var premiereReception = b.BonsReception
                        .Where(r => r.Statut == StatutBonReception.Valide)
                        .OrderBy(r => r.DateReception)
                        .FirstOrDefault();
                    return premiereReception != null &&
                           premiereReception.DateReception.Date <= b.DateLivraisonSouhaitee!.Value.Date;
                });
                scoreDelais = (double)respectes / bcsAvecReception.Count * 100;
            }

            // --- Stabilité des prix (40%) ---
            var historique = await _db.AchatHistoriquesPrix
                .Where(h => h.FournisseurId == fournisseurId)
                .OrderBy(h => h.DateAchat)
                .ToListAsync();

            double scoreStabilite = 100;
            if (historique.Count > 1)
            {
                var variations = new List<double>();
                for (int i = 1; i < historique.Count; i++)
                {
                    if (historique[i - 1].PrixUnitaireHT > 0)
                    {
                        double variation = Math.Abs(
                            (double)(historique[i].PrixUnitaireHT - historique[i - 1].PrixUnitaireHT)
                            / (double)historique[i - 1].PrixUnitaireHT * 100);
                        variations.Add(variation);
                    }
                }
                double variationMoyenne = variations.Any() ? variations.Average() : 0;
                // Variation < 5% = parfait, > 30% = score 0
                scoreStabilite = Math.Max(0, 100 - variationMoyenne * 3.33);
            }

            // --- Qualité livraisons (20%) ---
            var toutesLignes = bcs
                .SelectMany(b => b.BonsReception)
                .SelectMany(r => r.Lignes)
                .ToList();

            double scoreQualite = 100;
            if (toutesLignes.Any())
            {
                int conformes = toutesLignes.Count(l => l.Etat == EtatReceptionLigne.Conforme);
                scoreQualite = (double)conformes / toutesLignes.Count * 100;
            }

            // Score global pondéré
            double scoreGlobal = Math.Round(scoreDelais * 0.4 + scoreStabilite * 0.4 + scoreQualite * 0.2, 0);

            string niveau = scoreGlobal >= 75 ? "Excellent"
                          : scoreGlobal >= 50 ? "Moyen"
                          : "Mauvais";

            return new ScoreFournisseur
            {
                FournisseurId    = fournisseurId,
                Score            = (int)scoreGlobal,
                Niveau           = niveau,
                ScoreDelais      = (int)Math.Round(scoreDelais),
                ScoreStabilite   = (int)Math.Round(scoreStabilite),
                ScoreQualite     = (int)Math.Round(scoreQualite)
            };
        }

        // =====================================================================
        //  SIMULATEUR "ET SI ?"
        // =====================================================================

        /// <summary>
        /// Simule l'impact d'une hausse de prix d'un composant sur toutes les BOMs.
        /// Retourne la liste des produits finis impactés avec leur nouveau coût estimé.
        /// </summary>
        public async Task<List<ImpactSimulation>> SimulerHaussePrixAsync(int produitId, decimal pourcentageHausse)
        {
            var produit = await _db.Produits.FindAsync(produitId);
            if (produit == null) return new();

            decimal ancienPrix  = produit.CoutAchat;
            decimal nouveauPrix = Math.Round(ancienPrix * (1 + pourcentageHausse / 100), 2);
            decimal deltaUnitaire = nouveauPrix - ancienPrix;

            // Trouver toutes les BOMs qui utilisent ce composant
            var bomLignes = await _db.BomLignes
                .Include(l => l.Bom).ThenInclude(b => b.Produit)
                .Where(l => l.ComposantProduitId == produitId)
                .ToListAsync();

            var resultats = bomLignes.Select(l => new ImpactSimulation
            {
                ProduitFiniId     = l.Bom.ProduitId,
                NomProduitFini    = l.Bom.Produit?.Nom ?? "Inconnu",
                CoutActuel        = l.Bom.Produit?.CoutTotal ?? 0m,
                DeltaCout         = Math.Round(deltaUnitaire * l.Quantite, 2),
                NouveauCoutEstime = Math.Round((l.Bom.Produit?.CoutTotal ?? 0m) + deltaUnitaire * l.Quantite, 2),
                QuantiteUtilisee  = l.Quantite
            }).ToList();

            return resultats;
        }
    }

    // =====================================================================
    //  DTOs
    // =====================================================================

    public class HistoriquePrixGraphe
    {
        public int ProduitId { get; set; }
        public decimal PrixBomActuel { get; set; }
        public List<SeriesPrix> Series { get; set; } = new();
    }

    public class SeriesPrix
    {
        public int FournisseurId { get; set; }
        public string NomFournisseur { get; set; } = string.Empty;
        public List<PointPrix> Points { get; set; } = new();
    }

    public class PointPrix
    {
        public DateTime Date { get; set; }
        public decimal PrixHT { get; set; }
    }

    public class ScoreFournisseur
    {
        public int FournisseurId { get; set; }
        public int Score { get; set; }          // 0–100, -1 = pas assez de données
        public string Niveau { get; set; } = "Inconnu"; // Excellent / Moyen / Mauvais / Inconnu
        public int ScoreDelais { get; set; }
        public int ScoreStabilite { get; set; }
        public int ScoreQualite { get; set; }
    }

    public class ImpactSimulation
    {
        public int ProduitFiniId { get; set; }
        public string NomProduitFini { get; set; } = string.Empty;
        public decimal CoutActuel { get; set; }
        public decimal DeltaCout { get; set; }
        public decimal NouveauCoutEstime { get; set; }
        public decimal QuantiteUtilisee { get; set; }
    }
}
