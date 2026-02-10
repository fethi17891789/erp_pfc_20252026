// Fichier : Presentation/view/ProduitNew.cshtml.cs
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

        public string SuccessMessage { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public bool ReturnToBom { get; set; }
        public string InitialName { get; set; } = string.Empty;

        public string? BomTarget { get; set; }
        public int? BomRowIndex { get; set; }

        [BindProperty]
        public IFormFile? ProductImage { get; set; }

        public async Task OnGetAsync(int? id, string? name, bool? returnToBom, string? target, int? rowIndex)
        {
            ReturnToBom = returnToBom ?? false;
            InitialName = name ?? string.Empty;
            BomTarget = target;
            BomRowIndex = rowIndex;

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
                    ErrorMessage = "Produit introuvable.";
                    NouveauProduit = new Produit();
                }
            }
            else
            {
                Console.WriteLine("[DEBUG] ProduitNew.OnGetAsync - mode CREATION");
                NouveauProduit = new Produit();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    NouveauProduit.Nom = name;
                }

                NouveauProduit.QuantiteDisponible = 0m;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine("[DEBUG] ProduitNew.OnPostAsync appelé");

            var idValue = Request.Form["Id"].ToString();
            int.TryParse(idValue, out var id);

            var returnToBomRaw = Request.Form["ReturnToBom"].ToString();
            var initialNameRaw = Request.Form["InitialName"].ToString();
            ReturnToBom = !string.IsNullOrWhiteSpace(returnToBomRaw) &&
                          (returnToBomRaw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                           returnToBomRaw.Equals("on", StringComparison.OrdinalIgnoreCase));
            InitialName = initialNameRaw ?? string.Empty;

            BomTarget = Request.Query["target"].ToString();
            var rowIndexRaw = Request.Query["rowIndex"].ToString();
            if (int.TryParse(rowIndexRaw, out var rowIndexParsed))
                BomRowIndex = rowIndexParsed;
            else
                BomRowIndex = null;

            var productNameValue = Request.Form["ProductName"].ToString();
            var referenceValue = Request.Form["Reference"].ToString();
            var barcodeValue = Request.Form["Barcode"].ToString();
            var productTypeValue = Request.Form["ProductType"].ToString();
            var salePriceValue = Request.Form["SalePrice"].ToString();
            var costValueRaw = Request.Form["Cost"].ToString();
            var commentValue = Request.Form["Comment"].ToString();
            var isSaleableValue = Request.Form["IsSaleable"].ToString();
            var trackInvValue = Request.Form["TrackInventory"].ToString();
            var availableQtyValue = Request.Form["AvailableQuantity"].ToString();

            // Champs techniques
            var typeTechniqueRaw = Request.Form["TypeTechnique"].ToString();
            var coutAutresRaw = Request.Form["CoutAutresCharges"].ToString();
            var coutBomRaw = Request.Form["CoutBom"].ToString();
            var coutTotalRaw = Request.Form["CoutTotal"].ToString();

            NouveauProduit.Nom = productNameValue;
            NouveauProduit.Reference = referenceValue;
            NouveauProduit.CodeBarres = barcodeValue;
            NouveauProduit.Type = string.IsNullOrWhiteSpace(productTypeValue) ? "Bien" : productTypeValue;

            // Type technique (0=MP, 1=SemiFini, 2=Fini, 3=SemiFiniEtFini)
            if (!int.TryParse(typeTechniqueRaw, out var typeTecInt))
                typeTecInt = 0;
            NouveauProduit.TypeTechnique = (TypeTechniqueProduit)typeTecInt;

            // Prix de vente
            var parsedSalePrice = ParseDecimalFromForm(salePriceValue);

            // MP ou Semi-fini -> prix de vente forcé à 0
            if (NouveauProduit.TypeTechnique == TypeTechniqueProduit.SemiFini
                || NouveauProduit.TypeTechnique == TypeTechniqueProduit.MatierePremiere)
            {
                NouveauProduit.PrixVente = 0m;
            }
            else
            {
                NouveauProduit.PrixVente = parsedSalePrice;
            }

            // Coût d'achat de base :
            // si le champ est "none" (SF / Fini / SF+Fini), on force à 0.
            decimal parsedCost = 0m;
            if (!string.IsNullOrWhiteSpace(costValueRaw) &&
                !string.Equals(costValueRaw.Trim(), "none", StringComparison.OrdinalIgnoreCase))
            {
                parsedCost = ParseDecimalFromForm(costValueRaw);
            }

            // Coûts selon le type technique
            if (NouveauProduit.TypeTechnique == TypeTechniqueProduit.MatierePremiere)
            {
                // Matière première : on garde le coût d'achat saisi
                NouveauProduit.Cout = parsedCost;
                NouveauProduit.CoutAchat = parsedCost;
                // CoutBom vient du formulaire (souvent 0 pour MP)
                NouveauProduit.CoutBom = ParseDecimalFromForm(coutBomRaw);
            }
            else
            {
                // Semi-fini, Fini ou les deux : CoutAchat et CoutBom forcés à 0
                NouveauProduit.Cout = 0m;
                NouveauProduit.CoutAchat = 0m;
                NouveauProduit.CoutBom = 0m;
            }

            NouveauProduit.QuantiteDisponible = ParseDecimalFromForm(availableQtyValue);

            // Autres charges (toujours 0 via le champ caché)
            NouveauProduit.CoutAutresCharges = ParseDecimalFromForm(coutAutresRaw);

            // CoutTotal vient UNIQUEMENT du formulaire, pas de recalcul
            NouveauProduit.CoutTotal = ParseDecimalFromForm(coutTotalRaw);

            NouveauProduit.DisponibleVente = !string.IsNullOrEmpty(isSaleableValue) &&
                                             (isSaleableValue == "on" || isSaleableValue == "true");
            NouveauProduit.SuiviInventaire = !string.IsNullOrEmpty(trackInvValue) &&
                                             (trackInvValue == "on" || trackInvValue == "true");
            NouveauProduit.Notes = string.IsNullOrWhiteSpace(commentValue) ? null : commentValue;

            if (string.IsNullOrWhiteSpace(NouveauProduit.Nom))
            {
                ErrorMessage = "Le nom du produit est obligatoire.";
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
                        ErrorMessage = "Produit introuvable.";
                        return Page();
                    }

                    if (!string.IsNullOrWhiteSpace(NouveauProduit.Reference))
                    {
                        var refExiste = await _context.Produits
                            .AnyAsync(p => p.Reference == NouveauProduit.Reference && p.Id != id);

                        if (refExiste)
                        {
                            ErrorMessage = "Cette référence existe déjà pour un autre produit.";
                            return Page();
                        }
                    }

                    existing.Nom = NouveauProduit.Nom;
                    existing.Reference = NouveauProduit.Reference;
                    existing.CodeBarres = NouveauProduit.CodeBarres;
                    existing.Type = NouveauProduit.Type;
                    existing.PrixVente = NouveauProduit.PrixVente;
                    existing.Cout = NouveauProduit.Cout;
                    existing.QuantiteDisponible = NouveauProduit.QuantiteDisponible;
                    existing.DisponibleVente = NouveauProduit.DisponibleVente;
                    existing.SuiviInventaire = NouveauProduit.SuiviInventaire;
                    existing.Notes = NouveauProduit.Notes;

                    existing.TypeTechnique = NouveauProduit.TypeTechnique;
                    existing.CoutAchat = NouveauProduit.CoutAchat;
                    existing.CoutAutresCharges = NouveauProduit.CoutAutresCharges;
                    existing.CoutBom = NouveauProduit.CoutBom;
                    existing.CoutTotal = NouveauProduit.CoutTotal;

                    if (ProductImage != null && ProductImage.Length > 0)
                    {
                        var fileName = await SaveProductImageAsync(ProductImage);
                        existing.Image = fileName;
                        Console.WriteLine($"[DEBUG] ProduitNew.OnPost - nouvelle image enregistrée {fileName}");
                    }

                    await _context.SaveChangesAsync();
                    SuccessMessage = $"Produit {existing.Nom} mis à jour avec succès (ID {existing.Id}).";
                    NouveauProduit = existing;
                    return Page();
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
                            ErrorMessage = "Cette référence existe déjà.";
                            return Page();
                        }
                    }

                    if (ProductImage != null && ProductImage.Length > 0)
                    {
                        var fileName = await SaveProductImageAsync(ProductImage);
                        NouveauProduit.Image = fileName;
                        Console.WriteLine($"[DEBUG] ProduitNew.OnPost - image enregistrée {fileName}");
                    }

                    _context.Produits.Add(NouveauProduit);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"[DEBUG] ProduitNew.OnPost - produit créé ID={NouveauProduit.Id}, ReturnToBom={ReturnToBom}, BomTarget={BomTarget}, BomRowIndex={BomRowIndex}");

                    if (ReturnToBom)
                    {
                        if (!string.IsNullOrEmpty(BomTarget) &&
                            BomTarget.Equals("row", StringComparison.OrdinalIgnoreCase) &&
                            BomRowIndex.HasValue)
                        {
                            return RedirectToPage("BOMCreate", new { fromComponentId = NouveauProduit.Id, rowIndex = BomRowIndex.Value });
                        }

                        return RedirectToPage("BOMCreate", new { fromProductId = NouveauProduit.Id });
                    }

                    SuccessMessage = $"Produit {NouveauProduit.Nom} créé avec succès (ID {NouveauProduit.Id}).";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Erreur lors de l'enregistrement : {ex}");
                ErrorMessage = $"Erreur lors de l'enregistrement : {ex.Message}";
                return Page();
            }
        }

        private decimal ParseDecimalFromForm(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return 0m;

            var normalized = raw.Replace(',', '.').Trim();
            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                return value;

            Console.WriteLine($"[DEBUG] ParseDecimalFromForm - échec de parse pour valeur '{raw}', normalisée '{normalized}', valeur forcée à 0.");
            return 0m;
        }

        private async Task<string> SaveProductImageAsync(IFormFile file)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "produits");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".png";

            var uniqueName = "prod_" + Guid.NewGuid().ToString("N") + ext;
            var filePath = Path.Combine(uploadsFolder, uniqueName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return uniqueName;
        }
    }
}
