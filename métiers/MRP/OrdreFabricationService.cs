// Fichier : Metier/MRP/OrdreFabricationService.cs
using Donnees;
using erp_pfc_20252026.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Metier.MRP
{
    public class OrdreFabricationService
    {
        private readonly ErpDbContext _db;

        public OrdreFabricationService(ErpDbContext db)
        {
            _db = db;
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

            // Contenu texte « pseudo-PDF » (tu pourras le remplacer plus tard par une vraie génération PDF)
            var sb = new StringBuilder();
            sb.AppendLine("ORDRE DE FABRICATION");
            sb.AppendLine("=====================");
            sb.AppendLine($"Référence OF : {referenceOf}");
            sb.AppendLine($"Plan MRP     : {plan.Reference}");
            sb.AppendLine();
            sb.AppendLine($"Article      : {produit.Reference} - {produit.Nom}");
            sb.AppendLine($"Type         : {lignePlan.TypeProduit}");
            sb.AppendLine();
            sb.AppendLine($"Quantité OF  : {quantite}");
            sb.AppendLine($"Date ordre   : {DateTime.UtcNow:dd/MM/yyyy HH:mm}");
            sb.AppendLine();
            sb.AppendLine("Ce document est un exemple de contenu pour le PDF d'ordre de fabrication.");

            var pdfBytes = Encoding.UTF8.GetBytes(sb.ToString());

            var fichier = new MRPFichier
            {
                PlanificationId = plan.Id,
                CodeArticle = codeArticle,
                ReferenceOF = referenceOf,
                DateOrdre = DateTime.UtcNow,
                FichierNom = referenceOf + ".pdf",
                ContentType = "application/pdf",
                TailleOctets = pdfBytes.LongLength,
                FichierBlob = pdfBytes,
                CreeLe = DateTime.UtcNow
            };

            _db.MRPFichiers.Add(fichier);
            await _db.SaveChangesAsync();

            return fichier;
        }
    }
}
