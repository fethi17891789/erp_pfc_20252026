// Fichier : Metier/Achats/AchatsService.cs
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
    /// Service principal du module Achats.
    /// Gère les Bons de Commande, Bons de Réception, Factures fournisseurs,
    /// la numérotation automatique et la configuration du module.
    /// </summary>
    public class AchatsService
    {
        private readonly ErpDbContext _db;
        private const decimal TAUX_TVA = 0.19m;

        public AchatsService(ErpDbContext db)
        {
            _db = db;
        }

        // =====================================================================
        //  CONFIGURATION MODULE
        // =====================================================================

        /// <summary>
        /// Retourne la config du module. Si elle n'existe pas, retourne null
        /// (l'UI affichera l'écran de premier lancement).
        /// </summary>
        public async Task<AchatConfigModule?> GetConfigAsync()
        {
            return await _db.AchatConfigModules.FirstOrDefaultAsync();
        }

        /// <summary>
        /// Enregistre la politique de prix choisie au premier lancement.
        /// </summary>
        public async Task<AchatConfigModule> ConfigurerModuleAsync(PolitiquePrixAchat politique, int? userId)
        {
            var config = await _db.AchatConfigModules.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new AchatConfigModule
                {
                    PolitiquePrix  = politique,
                    EstConfigure   = true,
                    DateCreation   = DateTime.UtcNow,
                    CreeParUserId  = userId
                };
                _db.AchatConfigModules.Add(config);
            }
            else
            {
                config.PolitiquePrix = politique;
                config.EstConfigure  = true;
            }

            await _db.SaveChangesAsync();
            return config;
        }

        // =====================================================================
        //  NUMÉROTATION AUTOMATIQUE
        // =====================================================================

        /// <summary>Génère le prochain numéro BC pour l'année en cours : BC-2025-001</summary>
        public async Task<string> ProchainNumeroBCAsync()
            => await GenererNumeroAsync("BC", "AchatBonCommandes", "Numero");

        /// <summary>Génère le prochain numéro BR pour l'année en cours : BR-2025-001</summary>
        public async Task<string> ProchainNumeroBRAsync()
            => await GenererNumeroAsync("BR", "AchatBonReceptions", "Numero");

        /// <summary>Génère le prochain numéro FAC pour l'année en cours : FAC-2025-001</summary>
        public async Task<string> ProchainNumeroFACAsync()
            => await GenererNumeroAsync("FAC", "AchatFacturesFournisseur", "Numero");

        private async Task<string> GenererNumeroAsync(string prefixe, string table, string colonne)
        {
            int annee = DateTime.UtcNow.Year;
            string pattern = $"{prefixe}-{annee}-%";

            var sql = $@"
                SELECT COALESCE(MAX(CAST(SPLIT_PART(""{colonne}"", '-', 3) AS INTEGER)), 0)
                FROM ""{table}""
                WHERE ""{colonne}"" LIKE '{pattern}'";

            using var conn = new Npgsql.NpgsqlConnection(_db.Database.GetConnectionString());
            await conn.OpenAsync();
            using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync();
            int prochain = Convert.ToInt32(result) + 1;

            return $"{prefixe}-{annee}-{prochain:D3}";
        }

        // =====================================================================
        //  BONS DE COMMANDE
        // =====================================================================

        public async Task<List<AchatBonCommande>> GetBonCommandesAsync()
        {
            return await _db.AchatBonCommandes
                .Include(b => b.Fournisseur)
                .Include(b => b.Lignes).ThenInclude(l => l.Produit)
                .OrderByDescending(b => b.DateCreation)
                .ToListAsync();
        }

        public async Task<AchatBonCommande?> GetBonCommandeAsync(int id)
        {
            return await _db.AchatBonCommandes
                .Include(b => b.Fournisseur)
                .Include(b => b.Lignes).ThenInclude(l => l.Produit)
                .Include(b => b.Proformas)
                .Include(b => b.BonsReception).ThenInclude(r => r.Lignes).ThenInclude(l => l.Produit)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<AchatBonCommande?> GetBonCommandeParTokenAsync(string token)
        {
            return await _db.AchatBonCommandes
                .Include(b => b.Fournisseur)
                .Include(b => b.Lignes).ThenInclude(l => l.Produit)
                .FirstOrDefaultAsync(b => b.TokenConfirmation == token);
        }

        public async Task<AchatBonCommande> CreerBonCommandeAsync(
            int fournisseurId,
            DateTime? dateLivraison,
            string? notes,
            List<(int ProduitId, bool EstSousTraitance, decimal Quantite, decimal PrixHT)> lignes,
            int? userId)
        {
            var numero = await ProchainNumeroBCAsync();

            var bc = new AchatBonCommande
            {
                Numero                  = numero,
                FournisseurId           = fournisseurId,
                DateCommande            = DateTime.UtcNow,
                DateLivraisonSouhaitee  = dateLivraison,
                Notes                   = notes,
                Statut                  = StatutBonCommande.Brouillon,
                DateCreation            = DateTime.UtcNow,
                CreeParUserId           = userId
            };

            foreach (var (produitId, estST, qte, prixHT) in lignes)
            {
                bc.Lignes.Add(new AchatBonCommandeLigne
                {
                    ProduitId        = produitId,
                    EstSousTraitance = estST,
                    Quantite         = qte,
                    PrixUnitaireHT   = prixHT,
                    TotalHT          = Math.Round(qte * prixHT, 2)
                });
            }

            RecalculerTotaux(bc);

            _db.AchatBonCommandes.Add(bc);
            await _db.SaveChangesAsync();
            return bc;
        }

        /// <summary>
        /// Marque le BC comme envoyé et génère son token de confirmation.
        /// </summary>
        public async Task MarquerEnvoyeAsync(int bcId, DateTime? dateEnvoi = null)
        {
            var bc = await _db.AchatBonCommandes.FindAsync(bcId)
                ?? throw new Exception($"BC {bcId} introuvable.");

            bc.Statut            = StatutBonCommande.Envoye;
            bc.DateEnvoiMail     = dateEnvoi ?? DateTime.UtcNow;
            bc.TokenConfirmation = Guid.NewGuid().ToString("N");

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Traite la réponse du fournisseur depuis la page publique (via token).
        /// </summary>
        public async Task<bool> TraiterReponsFournisseurAsync(
            string token, bool confirme, string? message, DateTime? dateLivraisonProposee)
        {
            var bc = await GetBonCommandeParTokenAsync(token);
            if (bc == null) return false;

            bc.Statut                 = confirme ? StatutBonCommande.Confirme : StatutBonCommande.Refuse;
            bc.RepondeurMessage       = message;
            bc.DateReponse            = DateTime.UtcNow;
            bc.DateLivraisonProposee  = dateLivraisonProposee;

            // Invalider le token après usage pour sécurité
            bc.TokenConfirmation = null;

            await _db.SaveChangesAsync();
            return true;
        }

        private void RecalculerTotaux(AchatBonCommande bc)
        {
            bc.TotalHT    = Math.Round(bc.Lignes.Sum(l => l.TotalHT), 2);
            bc.MontantTVA = Math.Round(bc.TotalHT * TAUX_TVA, 2);
            bc.TotalTTC   = Math.Round(bc.TotalHT + bc.MontantTVA, 2);
        }

        // =====================================================================
        //  BONS DE RÉCEPTION
        // =====================================================================

        public async Task<AchatBonReception?> GetBonReceptionAsync(int id)
        {
            return await _db.AchatBonReceptions
                .Include(r => r.BonCommande).ThenInclude(b => b!.Fournisseur)
                .Include(r => r.Lignes).ThenInclude(l => l.Produit)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<AchatBonReception> CreerBonReceptionAsync(
            int bcId,
            DateTime dateReception,
            string? notes,
            List<(int ProduitId, decimal QteCommandee, decimal QteRecue, string Etat)> lignes,
            int? userId)
        {
            var numero = await ProchainNumeroBRAsync();

            var br = new AchatBonReception
            {
                Numero          = numero,
                BonCommandeId   = bcId,
                DateReception   = dateReception,
                Notes           = notes,
                Statut          = StatutBonReception.EnCours,
                DateCreation    = DateTime.UtcNow,
                CreeParUserId   = userId
            };

            foreach (var (produitId, qteCmd, qteRecue, etat) in lignes)
            {
                br.Lignes.Add(new AchatBonReceptionLigne
                {
                    ProduitId          = produitId,
                    QuantiteCommandee  = qteCmd,
                    QuantiteRecue      = qteRecue,
                    Etat               = etat
                });
            }

            _db.AchatBonReceptions.Add(br);
            await _db.SaveChangesAsync();
            return br;
        }

        /// <summary>
        /// Valide un BR :
        /// - Met à jour QuantiteDisponible de chaque produit reçu
        /// - Enregistre l'historique des prix
        /// - Met à jour le statut du BC
        /// </summary>
        public async Task ValiderBonReceptionAsync(int brId, int? userId)
        {
            var br = await _db.AchatBonReceptions
                .Include(r => r.Lignes)
                .Include(r => r.BonCommande).ThenInclude(b => b!.Lignes)
                .FirstOrDefaultAsync(r => r.Id == brId)
                ?? throw new Exception($"BR {brId} introuvable.");

            var config = await GetConfigAsync();
            var politique = config?.PolitiquePrix ?? PolitiquePrixAchat.DernierPrix;

            foreach (var ligne in br.Lignes.Where(l => l.QuantiteRecue > 0 && l.Etat == EtatReceptionLigne.Conforme))
            {
                var produit = await _db.Produits.FindAsync(ligne.ProduitId);
                if (produit == null) continue;

                // Mettre à jour la quantité disponible
                produit.QuantiteDisponible += ligne.QuantiteRecue;

                // Retrouver le prix unitaire HT depuis le BC
                var ligneBc = br.BonCommande?.Lignes.FirstOrDefault(l => l.ProduitId == ligne.ProduitId);
                decimal prixHT = ligneBc?.PrixUnitaireHT ?? 0m;

                if (prixHT > 0)
                {
                    // Mettre à jour le coût d'achat selon la politique choisie
                    if (politique == PolitiquePrixAchat.DernierPrix)
                    {
                        produit.CoutAchat = prixHT;
                    }
                    else
                    {
                        // Moyenne pondérée : (ancienPrix * anciensAchats + nouveauPrix * nouvellesQtes) / total
                        var totalExistant = await _db.AchatHistoriquesPrix
                            .Where(h => h.ProduitId == ligne.ProduitId)
                            .SumAsync(h => h.Quantite);
                        if (totalExistant > 0)
                        {
                            decimal sommeExistante = produit.CoutAchat * totalExistant;
                            decimal sommeNouvelle  = prixHT * ligne.QuantiteRecue;
                            produit.CoutAchat = Math.Round((sommeExistante + sommeNouvelle) / (totalExistant + ligne.QuantiteRecue), 2);
                        }
                        else
                        {
                            produit.CoutAchat = prixHT;
                        }
                    }

                    // Enregistrer dans l'historique des prix
                    _db.AchatHistoriquesPrix.Add(new AchatHistoriquePrix
                    {
                        ProduitId       = ligne.ProduitId,
                        FournisseurId   = br.BonCommande!.FournisseurId,
                        PrixUnitaireHT  = prixHT,
                        Quantite        = ligne.QuantiteRecue,
                        DateAchat       = DateTime.UtcNow,
                        BonCommandeId   = br.BonCommandeId
                    });
                }
            }

            // Mettre à jour le statut du BR
            br.Statut = StatutBonReception.Valide;

            // Mettre à jour le statut du BC
            var bc = br.BonCommande;
            if (bc != null)
            {
                var tousRecus = await _db.AchatBonReceptions
                    .Where(r => r.BonCommandeId == bc.Id && r.Statut == StatutBonReception.Valide)
                    .AnyAsync();
                bc.Statut = tousRecus ? StatutBonCommande.Recu : StatutBonCommande.PartiellemtRecu;
            }

            await _db.SaveChangesAsync();
        }

        // =====================================================================
        //  FACTURES FOURNISSEURS
        // =====================================================================

        public async Task<AchatFactureFournisseur?> GetFactureAsync(int id)
        {
            return await _db.AchatFacturesFournisseur
                .Include(f => f.BonCommande).ThenInclude(b => b!.Fournisseur)
                .Include(f => f.BonReception)
                .FirstOrDefaultAsync(f => f.Id == id);
        }

        public async Task<List<AchatFactureFournisseur>> GetFacturesParBCAsync(int bcId)
        {
            return await _db.AchatFacturesFournisseur
                .Where(f => f.BonCommandeId == bcId)
                .OrderByDescending(f => f.DateCreation)
                .ToListAsync();
        }

        public async Task<AchatFactureFournisseur> CreerFactureAsync(
            int bcId, int? brId, string? numeroFournisseur,
            DateTime dateFacture, decimal montantHT, string? notes, int? userId)
        {
            var bc = await _db.AchatBonCommandes.FindAsync(bcId)
                ?? throw new Exception($"BC {bcId} introuvable.");

            var numero = await ProchainNumeroFACAsync();

            decimal tva = Math.Round(montantHT * TAUX_TVA, 2);
            decimal ttc = Math.Round(montantHT + tva, 2);

            // Calcul de l'écart avec le BC
            decimal ecart = bc.TotalHT > 0
                ? Math.Abs(Math.Round((montantHT - bc.TotalHT) / bc.TotalHT * 100, 2))
                : 0m;

            var facture = new AchatFactureFournisseur
            {
                Numero              = numero,
                NumeroFournisseur   = numeroFournisseur,
                BonCommandeId       = bcId,
                BonReceptionId      = brId,
                DateFacture         = dateFacture,
                MontantHT           = montantHT,
                MontantTVA          = tva,
                MontantTTC          = ttc,
                AlerteEcartPrix     = ecart > 2m,
                EcartPourcentage    = ecart,
                Statut              = StatutFactureFournisseur.Recue,
                Notes               = notes,
                DateCreation        = DateTime.UtcNow,
                CreeParUserId       = userId
            };

            _db.AchatFacturesFournisseur.Add(facture);

            // Mettre à jour le statut du BC
            bc.Statut = StatutBonCommande.Facture;

            await _db.SaveChangesAsync();
            return facture;
        }

        // =====================================================================
        //  KPI POUR LE HOME
        // =====================================================================

        public async Task<int> CompterBCEnCoursAsync()
            => await _db.AchatBonCommandes.CountAsync(b => b.Statut == StatutBonCommande.Envoye);

        public async Task<int> CompterBCRefusesAsync()
            => await _db.AchatBonCommandes.CountAsync(b => b.Statut == StatutBonCommande.Refuse);
    }
}
