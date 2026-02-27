using System;

namespace erp_pfc_20252026.Data.Entities
{
    public class MRPFichier
    {
        public int Id { get; set; }

        // Planification MRP à laquelle ce fichier est lié
        public int PlanificationId { get; set; }

        // Code article (produit fini concerné par l’OF)
        public string CodeArticle { get; set; } = string.Empty;

        // Référence d’ordre de fabrication (OF-0001, etc.)
        public string ReferenceOF { get; set; } = string.Empty;

        // Date de l’ordre de fabrication
        public DateTime DateOrdre { get; set; }

        // Nom de fichier (OF-0001.pdf)
        public string FichierNom { get; set; } = string.Empty;

        // Données binaires du PDF stockées dans PostgreSQL
        public byte[] FichierBlob { get; set; } = Array.Empty<byte>();

        // Métadonnées techniques (facultatives)
        public string ContentType { get; set; } = "application/pdf";
        public long TailleOctets { get; set; }

        // Date de création
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        // Navigation optionnelle vers la planification (si tu as une entité PlanificationMrp)
        // public virtual PlanificationMrp Planification { get; set; }
    }
}
