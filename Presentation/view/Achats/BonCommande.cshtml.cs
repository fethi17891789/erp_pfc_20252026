// Fichier : Presentation/view/Achats/BonCommande.cshtml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Donnees;
using Donnees.Achats;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages.Achats
{
    public class BonCommandeModel : PageModel
    {
        private readonly AchatsService      _achatsService;
        private readonly AchatsMailService  _mailService;
        private readonly AchatsGmailService _gmailService;
        private readonly ErpDbContext       _db;

        public BonCommandeModel(AchatsService achatsService, AchatsMailService mailService,
                                AchatsGmailService gmailService, ErpDbContext db)
        {
            _achatsService = achatsService;
            _mailService   = mailService;
            _gmailService  = gmailService;
            _db            = db;
        }

        // ─── Données d'affichage ───────────────────────────────────────────────
        public AchatBonCommande? BonCommande { get; set; }
        public List<Contact> Fournisseurs { get; set; } = new();
        public List<ProduitLigne> Produits { get; set; } = new();
        public List<AchatFactureFournisseur> Factures { get; set; } = new();
        public bool ModeCreation => BonCommande == null;

        // Message de succès transmis depuis une action POST
        public string? MessageSucces { get; set; }

        public class ProduitLigne
        {
            public int Id { get; set; }
            public string Nom { get; set; } = string.Empty;
            public string Reference { get; set; } = string.Empty;
            public decimal CoutAchat { get; set; }
            public bool EstMatierePremiere { get; set; }
            public TypeTechniqueProduit TypeTechnique { get; set; }
            public string TypeLabel => TypeTechnique switch
            {
                TypeTechniqueProduit.MatierePremiere => "Matière 1ère",
                TypeTechniqueProduit.SemiFini        => "Semi-fini",
                TypeTechniqueProduit.Fini            => "Fini",
                TypeTechniqueProduit.SemiFiniEtFini  => "Semi-fini & Fini",
                _                                    => "Produit"
            };
        }

        // ─── Champs du formulaire de création ─────────────────────────────────
        [BindProperty] public int FournisseurId { get; set; }
        [BindProperty] public string? DateLivraisonSouhaitee { get; set; }
        [BindProperty] public string? Notes { get; set; }
        [BindProperty] public string LignesJson { get; set; } = "[]";

        // ─── GET ───────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            var config = await _achatsService.GetConfigAsync();
            if (config?.EstConfigure != true)
                return RedirectToPage("/Achats/Config");

            await ChargerDonneesFormulaire();

            if (id.HasValue)
            {
                BonCommande = await _achatsService.GetBonCommandeAsync(id.Value);
                if (BonCommande == null) return NotFound();

                // Charger les factures liées à ce BC
                Factures = await _achatsService.GetFacturesParBCAsync(id.Value);
            }

            if (TempData["Succes"] is string msg) MessageSucces = msg;

            return Page();
        }

        // ─── POST : Créer un BC ────────────────────────────────────────────────
        public async Task<IActionResult> OnPostCreerAsync()
        {
            int? userId = HttpContext.Session.GetInt32("CurrentUserId");

            // Désérialiser les lignes envoyées en JSON depuis le formulaire
            var lignesInput = new List<(int ProduitId, bool EstST, decimal Qte, decimal PrixHT)>();
            try
            {
                var raw = JsonSerializer.Deserialize<List<LigneJson>>(LignesJson ?? "[]") ?? new();
                foreach (var l in raw)
                    lignesInput.Add((l.produitId, l.estSousTraitance, l.quantite, l.prixHT));
            }
            catch { /* lignes vides */ }

            if (!lignesInput.Any())
            {
                TempData["Erreur"] = "Le bon de commande doit contenir au moins une ligne.";
                return RedirectToPage();
            }

            DateTime? dateLivraison = null;
            if (!string.IsNullOrEmpty(DateLivraisonSouhaitee) && DateTime.TryParse(DateLivraisonSouhaitee, out var dl))
                dateLivraison = dl;

            var bc = await _achatsService.CreerBonCommandeAsync(
                FournisseurId, dateLivraison, Notes, lignesInput, userId);

            TempData["Succes"] = $"Bon de commande {bc.Numero} créé avec succès.";
            return RedirectToPage(new { id = bc.Id });
        }

        // ─── POST : Envoyer le BC au fournisseur ───────────────────────────────
        public async Task<IActionResult> OnPostEnvoyerAsync(int id)
        {
            var bc = await _achatsService.GetBonCommandeAsync(id);
            if (bc == null) return NotFound();

            await _achatsService.MarquerEnvoyeAsync(id);

            // Recharger pour avoir les lignes complètes
            bc = await _achatsService.GetBonCommandeAsync(id);

            string? emailFournisseur = bc!.Fournisseur?.Email;

            if (!string.IsNullOrEmpty(emailFournisseur))
            {
                try
                {
                    if (await _gmailService.EstConfigureAsync())
                    {
                        // ── Envoi via Gmail API (OAuth2) ──────────────────────
                        await _gmailService.EnvoyerBonCommandeAsync(bc, emailFournisseur);
                        TempData["Succes"] = $"✅ BC {bc.Numero} envoyé à {emailFournisseur}. " +
                            $"Le fournisseur peut confirmer/refuser par email — réponse détectée automatiquement.";
                    }
                    else
                    {
                        // ── Fallback SMTP ─────────────────────────────────────
                        string baseUrl = $"{Request.Scheme}://{Request.Host}";
                        await _mailService.EnvoyerBonCommandeAsync(bc, emailFournisseur, baseUrl);
                        TempData["Succes"] = $"✅ BC {bc.Numero} envoyé à {emailFournisseur}.";
                    }
                }
                catch (Exception ex)
                {
                    TempData["Succes"] = $"BC {bc.Numero} marqué comme envoyé. " +
                        $"Envoi email échoué : {ex.Message} — " +
                        $"Configurez l'email depuis Paramètres → Email sortant.";
                }
            }
            else
            {
                TempData["Succes"] = $"BC {bc.Numero} marqué comme envoyé. " +
                    $"Aucun email renseigné pour ce fournisseur.";
            }

            return RedirectToPage(new { id });
        }

        // ─── POST : Supprimer un BC brouillon ─────────────────────────────────
        public async Task<IActionResult> OnPostSupprimerAsync(int id)
        {
            var bc = await _db.AchatBonCommandes.FindAsync(id);
            if (bc != null && bc.Statut == StatutBonCommande.Brouillon)
            {
                _db.AchatBonCommandes.Remove(bc);
                await _db.SaveChangesAsync();
            }
            TempData["Succes"] = "Bon de commande supprimé.";
            return RedirectToPage("/Achats/Index");
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private async Task ChargerDonneesFormulaire()
        {
            // Uniquement les contacts de type Fournisseur
            Fournisseurs = await _db.Contacts
                .Where(c => (c.Roles & ContactRole.Fournisseur) == ContactRole.Fournisseur)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            Produits = await _db.Produits
                .OrderBy(p => p.Nom)
                .Select(p => new ProduitLigne
                {
                    Id                = p.Id,
                    Nom               = p.Nom,
                    Reference         = p.Reference,
                    CoutAchat         = p.CoutAchat,
                    EstMatierePremiere = p.TypeTechnique == TypeTechniqueProduit.MatierePremiere,
                    TypeTechnique      = p.TypeTechnique
                })
                .ToListAsync();
        }

        // ─── DTO interne pour désérialisation JSON des lignes ─────────────────
        private class LigneJson
        {
            public int produitId { get; set; }
            public bool estSousTraitance { get; set; }
            public decimal quantite { get; set; }
            public decimal prixHT { get; set; }
        }

        // ─── Helpers vue ──────────────────────────────────────────────────────
        public static (string bg, string color, string label) GetStatutStyle(string statut) => statut switch
        {
            StatutBonCommande.Brouillon        => ("rgba(56,189,248,0.12)",  "#38bdf8",  "Brouillon"),
            StatutBonCommande.Envoye           => ("rgba(251,191,36,0.12)",  "#fbbf24",  "Envoyé"),
            StatutBonCommande.Confirme         => ("rgba(34,197,94,0.12)",   "#22c55e",  "Confirmé"),
            StatutBonCommande.PartiellemtRecu  => ("rgba(168,85,247,0.12)",  "#a855f7",  "Partiellement reçu"),
            StatutBonCommande.Recu             => ("rgba(34,197,94,0.15)",   "#4ade80",  "Reçu"),
            StatutBonCommande.Facture          => ("rgba(192,132,252,0.12)", "#c084fc",  "Facturé"),
            StatutBonCommande.Refuse           => ("rgba(239,68,68,0.12)",   "#ef4444",  "Refusé"),
            _                                  => ("rgba(255,255,255,0.08)", "#a4a7c8",  statut)
        };

        public static (string bg, string color, string label) GetFactureStatutStyle(string statut) => statut switch
        {
            StatutFactureFournisseur.Recue         => ("rgba(56,189,248,0.10)",  "#38bdf8", "Reçue"),
            StatutFactureFournisseur.Verifiee      => ("rgba(34,197,94,0.10)",   "#22c55e", "Vérifiée"),
            StatutFactureFournisseur.Comptabilisee => ("rgba(192,132,252,0.10)", "#c084fc", "Comptabilisée"),
            _                                      => ("rgba(255,255,255,0.06)", "#a4a7c8", statut)
        };
    }
}
