// Fichier : Metier/MRP/OrdreFabricationService.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Donnees;
using erp_pfc_20252026.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Metier; // pour IPdfService

namespace Metier.MRP
{
    public class OrdreFabricationService
    {
        private readonly ErpDbContext _db;
        private readonly IPdfService _pdfService;

        public OrdreFabricationService(ErpDbContext db, IPdfService pdfService)
        {
            _db = db;
            _pdfService = pdfService;
        }

        public class OrdreFabricationPdfModel
        {
            public string ReferenceOF { get; set; } = string.Empty;
            public string ReferencePlan { get; set; } = string.Empty;
            public DateTime DateOrdre { get; set; }

            public string CodeArticle { get; set; } = string.Empty;
            public string NomArticle { get; set; } = string.Empty;
            public string TypeProduit { get; set; } = string.Empty;

            public decimal Quantite { get; set; }
            public DateTime DateHorizonDebut { get; set; }
            public DateTime DateHorizonFin { get; set; }

            public decimal CoutBom { get; set; }
            public decimal CoutTotalTheorique { get; set; }
        }

        public async Task<MRPFichier> GenererOrdreFabricationAsync(
            int planificationId,
            string codeArticle,
            decimal quantite)
        {
            if (string.IsNullOrWhiteSpace(codeArticle))
                throw new ArgumentException("Code article obligatoire.", nameof(codeArticle));

            if (quantite <= 0)
                throw new ArgumentException("La quantité doit être > 0.", nameof(quantite));

            var plan = await _db.MRPPlans
                .Include(p => p.Lignes)
                    .ThenInclude(l => l.Produit)
                .FirstOrDefaultAsync(p => p.Id == planificationId);

            if (plan == null)
                throw new InvalidOperationException("Planification MRP introuvable.");

            var lignePlan = plan.Lignes.FirstOrDefault(l => l.Produit.Reference == codeArticle);
            if (lignePlan == null)
                throw new InvalidOperationException("Ligne MRP introuvable pour cet article.");

            var produit = lignePlan.Produit;
            if (produit == null)
                throw new InvalidOperationException("Produit introuvable pour cet article.");

            var referenceOf = $"OF-{plan.Id:D4}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var dateOrdre = DateTime.UtcNow;

            var model = new OrdreFabricationPdfModel
            {
                ReferenceOF = referenceOf,
                ReferencePlan = plan.Reference,
                DateOrdre = dateOrdre,
                CodeArticle = produit.Reference,
                NomArticle = produit.Nom,
                TypeProduit = lignePlan.TypeProduit,
                Quantite = quantite,
                DateHorizonDebut = plan.DateDebutHorizon,
                DateHorizonFin = plan.DateFinHorizon,
                CoutBom = produit.CoutBom,
                CoutTotalTheorique = produit.CoutBom * quantite
            };

            // Génération du PDF via la vue Razor Pages/MRP/OFTemplate.cshtml
            var pdfBytes = await _pdfService.GeneratePdfFromViewAsync("MRP/OFTemplate", model);

            var fichier = new MRPFichier
            {
                PlanificationId = plan.Id,
                CodeArticle = codeArticle,
                ReferenceOF = referenceOf,
                DateOrdre = dateOrdre,
                FichierNom = referenceOf + ".pdf",
                ContentType = "application/pdf",
                TailleOctets = pdfBytes.LongLength,
                FichierBlob = pdfBytes,
                CreeLe = dateOrdre
            };

            _db.MRPFichiers.Add(fichier);
            await _db.SaveChangesAsync();

            return fichier;
        }
    }
}
