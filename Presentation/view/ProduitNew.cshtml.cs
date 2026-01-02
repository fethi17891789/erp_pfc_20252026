using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Donnees;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

namespace erp_pfc_20252026.Pages
{
    public class ProduitNewModel : PageModel
    {
        private readonly ErpDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProduitNewModel(ErpDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public Produit NouveauProduit { get; set; } = new Produit();

        public string Message { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile? ProductImage { get; set; }

        // GET avec id optionnel
        public async Task OnGetAsync(int? id)
        {
            if (id.HasValue)
            {
                Console.WriteLine($"[DEBUG] ProduitNew.OnGetAsync - mode EDIT, id={id.Value}");
                var produit = await _context.Produits.FirstOrDefaultAsync(p => p.Id == id.Value);
                if (produit != null)
                {
                    NouveauProduit = produit;
                }
                else
                {
                    Message = "Produit introuvable.";
                    NouveauProduit = new Produit();
                }
            }
            else
            {
                Console.WriteLine("[DEBUG] ProduitNew.OnGetAsync - mode CREATION");
                NouveauProduit = new Produit();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine("[DEBUG] ProduitNew.OnPostAsync appelé");

            var idValue = Request.Form["Id"].ToString();
            int.TryParse(idValue, out var id);

            var productNameValue = Request.Form["ProductName"].ToString();
            var referenceValue = Request.Form["Reference"].ToString();
            var barcodeValue = Request.Form["Barcode"].ToString();
            var productTypeValue = Request.Form["ProductType"].ToString();
            var salePriceValue = Request.Form["SalePrice"].ToString();
            var costValue = Request.Form["Cost"].ToString();
            var commentValue = Request.Form["Comment"].ToString();
            var isSaleableValue = Request.Form["IsSaleable"].ToString();
            var trackInvValue = Request.Form["TrackInventory"].ToString();

            NouveauProduit.Nom = productNameValue;
            NouveauProduit.Reference = referenceValue;
            NouveauProduit.CodeBarres = barcodeValue;
            NouveauProduit.Type = string.IsNullOrWhiteSpace(productTypeValue) ? "Bien" : productTypeValue;

            NouveauProduit.PrixVente = ParseDecimalFromForm(salePriceValue);
            NouveauProduit.Cout = ParseDecimalFromForm(costValue);

            NouveauProduit.DisponibleVente = !string.IsNullOrEmpty(isSaleableValue) && (isSaleableValue == "on" || isSaleableValue == "true");
            NouveauProduit.SuiviInventaire = !string.IsNullOrEmpty(trackInvValue) && (trackInvValue == "on" || trackInvValue == "true");
            NouveauProduit.Notes = string.IsNullOrWhiteSpace(commentValue) ? null : commentValue;

            if (string.IsNullOrWhiteSpace(NouveauProduit.Nom))
            {
                Message = "Le nom du produit est obligatoire.";
                return Page();
            }

            try
            {
                if (id > 0)
                {
                    Console.WriteLine($"[DEBUG] ProduitNew.OnPost - MODE EDITION pour id={id}");
                    var existing = await _context.Produits.FirstOrDefaultAsync(p => p.Id == id);
                    if (existing == null)
                    {
                        Message = "Produit introuvable.";
                        return Page();
                    }

                    if (!string.IsNullOrWhiteSpace(NouveauProduit.Reference))
                    {
                        var refExiste = await _context.Produits
                            .AnyAsync(p => p.Reference == NouveauProduit.Reference && p.Id != id);

                        if (refExiste)
                        {
                            Message = "Cette référence existe déjà pour un autre produit.";
                            return Page();
                        }
                    }

                    existing.Nom = NouveauProduit.Nom;
                    existing.Reference = NouveauProduit.Reference;
                    existing.CodeBarres = NouveauProduit.CodeBarres;
                    existing.Type = NouveauProduit.Type;
                    existing.PrixVente = NouveauProduit.PrixVente;
                    existing.Cout = NouveauProduit.Cout;
                    existing.DisponibleVente = NouveauProduit.DisponibleVente;
                    existing.SuiviInventaire = NouveauProduit.SuiviInventaire;
                    existing.Notes = NouveauProduit.Notes;

                    // Image : si un nouveau fichier est fourni, on l’enregistre et on remplace le nom
                    if (ProductImage != null && ProductImage.Length > 0)
                    {
                        var fileName = await SaveProductImageAsync(ProductImage);
                        existing.Image = fileName;
                        Console.WriteLine($"[DEBUG] ProduitNew.OnPost - nouvelle image enregistrée : {fileName}");
                    }

                    await _context.SaveChangesAsync();
                    Message = $"Produit '{existing.Nom}' mis à jour avec succès (ID = {existing.Id}).";
                    NouveauProduit = existing;
                }
                else
                {
                    Console.WriteLine("[DEBUG] ProduitNew.OnPost - MODE CREATION");

                    if (!string.IsNullOrWhiteSpace(NouveauProduit.Reference))
                    {
                        var refExiste = await _context.Produits
                            .AnyAsync(p => p.Reference == NouveauProduit.Reference);

                        if (refExiste)
                        {
                            Message = "Cette référence existe déjà.";
                            return Page();
                        }
                    }

                    if (ProductImage != null && ProductImage.Length > 0)
                    {
                        var fileName = await SaveProductImageAsync(ProductImage);
                        NouveauProduit.Image = fileName;
                        Console.WriteLine($"[DEBUG] ProduitNew.OnPost - image enregistrée : {fileName}");
                    }

                    _context.Produits.Add(NouveauProduit);
                    await _context.SaveChangesAsync();

                    Message = $"Produit '{NouveauProduit.Nom}' créé avec succès (ID = {NouveauProduit.Id}).";
                    // on recharge le produit pour avoir l’image en édition plutôt que vider le formulaire
                    return RedirectToPage(new { id = NouveauProduit.Id });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Erreur OnPost ProduitNew : {ex}");
                Message = $"Erreur lors de l'enregistrement : {ex.Message}";
            }

            return Page();
        }

        private decimal ParseDecimalFromForm(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return 0m;

            var normalized = raw.Replace(',', '.').Trim();

            if (decimal.TryParse(
                    normalized,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                return value;
            }

            Console.WriteLine($"[DEBUG] ParseDecimalFromForm - échec de parse pour valeur '{raw}' (normalisée '{normalized}'), valeur forcée à 0.");
            return 0m;
        }

        private async Task<string> SaveProductImageAsync(IFormFile file)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "produits");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".png";

            var uniqueName = $"prod_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsFolder, uniqueName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return uniqueName; // ce nom sera stocké dans Produit.Image
        }
    }
}
