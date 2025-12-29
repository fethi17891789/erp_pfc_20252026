using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages
{
    public class BOMModel : PageModel
    {
        // Représente une ligne de nomenclature (affichage)
        public class BomItem
        {
            public string CodeProduit { get; set; } = string.Empty;
            public string NomProduit { get; set; } = string.Empty;
            public string CodeBom { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
        }

        // Liste des BOM à afficher dans le tableau
        public List<BomItem> Boms { get; set; } = new List<BomItem>();

        public void OnGet()
        {
            // Pour l’instant, on laisse vide.
            // Plus tard tu rempliras Boms depuis la base.
            // Exemple de données de test (à décommenter si tu veux voir le tableau) :
            /*
            Boms = new List<BomItem>
            {
                new BomItem { CodeProduit = "P-001", NomProduit = "Produit A", CodeBom = "BOM-001", Version = "1.0" },
                new BomItem { CodeProduit = "P-002", NomProduit = "Produit B", CodeBom = "BOM-002", Version = "1.1" }
            };
            */
        }
    }
}
