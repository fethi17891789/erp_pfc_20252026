using Donnees;
using Metier.CRM;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;

namespace erp_pfc_20252026.Pages
{
    public class AnnuaireListModel : PageModel
    {
        private readonly AnnuaireService _annuaireService;

        public AnnuaireListModel(AnnuaireService annuaireService)
        {
            _annuaireService = annuaireService;
        }

        public List<Contact> Contacts { get; set; } = new();
        public List<ContactRelation> Relations { get; set; } = new();

        public int TotalContacts { get; set; }
        public int NbClients { get; set; }
        public int NbFournisseurs { get; set; }
        public int NbEmployes { get; set; }
        public int NbPartenaires { get; set; }
        public int NbInvestisseurs { get; set; }

        public async Task OnGetAsync()
        {
            Contacts = await _annuaireService.GetAllContactsAsync();
            Relations = await _annuaireService.GetAllRelationsAsync();

            TotalContacts   = Contacts.Count;
            NbClients       = Contacts.Count(c => c.Roles.HasFlag(ContactRole.Client));
            NbFournisseurs  = Contacts.Count(c => c.Roles.HasFlag(ContactRole.Fournisseur));
            NbEmployes      = Contacts.Count(c => c.Roles.HasFlag(ContactRole.Employe));
            NbPartenaires   = Contacts.Count(c => c.Roles.HasFlag(ContactRole.Partenaire));
            NbInvestisseurs = Contacts.Count(c => c.Roles.HasFlag(ContactRole.Investisseur));
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            await _annuaireService.DeleteContactAsync(id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            var contacts = await _annuaireService.GetAllContactsAsync();
            var sb = new StringBuilder();
            sb.AppendLine("Nom,Email,Téléphone,Site Web,Rôles,Date Création");
            foreach (var c in contacts)
            {
                var roles = c.Roles == ContactRole.None ? "" : c.Roles.ToString();
                sb.AppendLine($"\"{Esc(c.FullName)}\",\"{Esc(c.Email)}\",\"{Esc(c.Phone)}\",\"{Esc(c.Website)}\",\"{roles}\",\"{c.DateCreation:dd/MM/yyyy}\"");
            }
            var bom = Encoding.UTF8.GetPreamble();
            var content = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bom.Concat(content).ToArray(), "text/csv; charset=utf-8",
                        $"contacts_{DateTime.Now:yyyyMMdd}.csv");
        }

        private static string Esc(string? s) => (s ?? "").Replace("\"", "\"\"");
    }
}
