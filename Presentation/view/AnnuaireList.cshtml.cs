using Donnees;
using Metier.CRM;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace erp_pfc_20252026.Pages
{
    public class AnnuaireListModel : PageModel
    {
        private readonly AnnuaireService _annuaireService;

        public AnnuaireListModel(AnnuaireService annuaireService)
        {
            _annuaireService = annuaireService;
        }

        public List<Contact> Contacts { get; set; } = new List<Contact>();

        public async Task OnGetAsync()
        {
            Contacts = await _annuaireService.GetAllContactsAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            await _annuaireService.DeleteContactAsync(id);
            return RedirectToPage();
        }
    }
}
