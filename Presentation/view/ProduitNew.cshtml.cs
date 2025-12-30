using System;
using System.Threading.Tasks;
using Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages
{
    public class ProduitNewModel : PageModel
    {
        private readonly ErpDbContext _context;

        public ProduitNewModel(ErpDbContext context)
        {
            _context = context;
        }

        public Produit NouveauProduit { get; set; } = new Produit();

        public string Message { get; set; } = string.Empty;

        public void OnGet()
        {
            // Préparation éventuelle
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine("[DEBUG] ProduitNew.OnPostAsync appelé");

            // Récupération brute des valeurs du formulaire
            var productNameValue = Request.Form["ProductName"].ToString();
            var referenceValue = Request.Form["Reference"].ToString();
            var barcodeValue = Request.Form["Barcode"].ToString();
            var productTypeValue = Request.Form["ProductType"].ToString();
            var salePriceValue = Request.Form["SalePrice"].ToString();
            var costValue = Request.Form["Cost"].ToString();
            var commentValue = Request.Form["Comment"].ToString();
            var isSaleableValue = Request.Form["IsSaleable"].ToString();
            var trackInvValue = Request.Form["TrackInventory"].ToString();

            Console.WriteLine($"[DEBUG] Valeurs brutes du formulaire:");
            Console.WriteLine($"  ProductName={productNameValue}");
            Console.WriteLine($"  Reference={referenceValue}");
            Console.WriteLine($"  SalePrice={salePriceValue}");
            Console.WriteLine($"  Cost={costValue}");
            Console.WriteLine($"  IsSaleable={isSaleableValue}");
            Console.WriteLine($"  TrackInventory={trackInvValue}");

            // Mapping vers l'entité Produit
            NouveauProduit.Nom = productNameValue;
            NouveauProduit.Reference = referenceValue;
            NouveauProduit.CodeBarres = barcodeValue;
            NouveauProduit.Type = string.IsNullOrWhiteSpace(productTypeValue) ? "Bien" : productTypeValue;

            if (!decimal.TryParse(salePriceValue, out var prixVente))
                prixVente = 0m;
            if (!decimal.TryParse(costValue, out var cout))
                cout = 0m;

            NouveauProduit.PrixVente = prixVente;
            NouveauProduit.Cout = cout;

            // IMPORTANT : Remplir les booléens correctement
            // Les checkboxes non cochées n'envoient rien, donc on vérifie la valeur
            NouveauProduit.DisponibleVente = !string.IsNullOrEmpty(isSaleableValue) && (isSaleableValue == "on" || isSaleableValue == "true");
            NouveauProduit.SuiviInventaire = !string.IsNullOrEmpty(trackInvValue) && (trackInvValue == "on" || trackInvValue == "true");

            NouveauProduit.Notes = string.IsNullOrWhiteSpace(commentValue) ? null : commentValue;
            // DateCreation n'est PAS rempli ici - PostgreSQL le fera automatiquement avec NOW()

            Console.WriteLine($"[DEBUG] ProduitNew.OnPost - Nom={NouveauProduit.Nom}, Ref={NouveauProduit.Reference}, Prix={NouveauProduit.PrixVente}, Cout={NouveauProduit.Cout}");
            Console.WriteLine($"[DEBUG] ProduitNew.OnPost - DisponibleVente={NouveauProduit.DisponibleVente}, SuiviInventaire={NouveauProduit.SuiviInventaire}");

            // Validation manuelle
            if (string.IsNullOrWhiteSpace(NouveauProduit.Nom))
            {
                Message = "Le nom du produit est obligatoire.";
                Console.WriteLine("[DEBUG] ProduitNew.OnPost - Nom vide, validation échouée.");
                return Page();
            }

            try
            {
                // Vérifier doublon de référence
                if (!string.IsNullOrWhiteSpace(NouveauProduit.Reference))
                {
                    var refExiste = await _context.Produits
                        .AnyAsync(p => p.Reference == NouveauProduit.Reference);

                    Console.WriteLine($"[DEBUG] ProduitNew.OnPost - Ref existe déjà ? {refExiste}");

                    if (refExiste)
                    {
                        Message = "Cette référence existe déjà.";
                        return Page();
                    }
                }

                Console.WriteLine("[DEBUG] ProduitNew.OnPost - Avant insert en base.");
                _context.Produits.Add(NouveauProduit);
                var rows = await _context.SaveChangesAsync();
                Console.WriteLine($"[DEBUG] ProduitNew.OnPost - SaveChangesAsync => {rows} ligne(s) affectée(s). ID = {NouveauProduit.Id}");

                Message = $"Produit '{NouveauProduit.Nom}' créé avec succès (ID = {NouveauProduit.Id}).";

                // Réinitialiser le formulaire
                NouveauProduit = new Produit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Erreur OnPost ProduitNew : {ex}");
                Message = $"Erreur lors de la création : {ex.Message}";
            }

            return Page();
        }
    }
}
