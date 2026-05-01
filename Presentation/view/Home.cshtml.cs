// Fichier : Presentation/view/Home.cshtml.cs
using System.Threading.Tasks;
using Donnees;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace erp_pfc_20252026.Pages
{
    public class HomeModel : PageModel
    {
        private readonly ErpDbContext _context;

        public HomeModel(ErpDbContext context)
        {
            _context = context;
        }

        public int NbProduits { get; set; }
        public int NbContacts { get; set; }
        public int NbVehiculesEnRoute { get; set; }
        public int NbVehiculesTotal { get; set; }

        public async Task OnGetAsync()
        {
            NbProduits = await _context.Produits.CountAsync();
            NbContacts = await _context.Contacts.CountAsync();
            NbVehiculesTotal = await _context.LogistiqueVehicules.CountAsync();
            NbVehiculesEnRoute = await _context.LogistiqueVehicules
                .CountAsync(v => v.Statut == "En Trajet");
        }
    }
}
