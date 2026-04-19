using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Metier.Logistique;
using Donnees.Logistique;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

namespace erp_pfc_20252026.Pages.Logistique
{
    public class EnvironnementModel : PageModel
    {
        private readonly LogistiqueService _logistiqueService;
        private readonly Metier.IPdfService _pdfService;

        public EnvironnementModel(LogistiqueService logistiqueService, Metier.IPdfService pdfService)
        {
            _logistiqueService = logistiqueService;
            _pdfService = pdfService;
        }

        public StatistiquesRSE Stats { get; set; } = new();
        public List<Trajet> DerniersTrajetsCO2 { get; set; } = new();
        public List<Vehicule> Vehicules { get; set; } = new();

        public async Task OnGetAsync()
        {
            Stats = await _logistiqueService.GetStatistiquesRSEAsync();
            DerniersTrajetsCO2 = await _logistiqueService.GetTrajetsAvecCO2Async(15);
            Vehicules = await _logistiqueService.GetVehiculesAsync();
        }

        public async Task<IActionResult> OnGetExportPdfAsync()
        {
            Stats = await _logistiqueService.GetStatistiquesRSEAsync();

            var html = GeneratePdfHtml(Stats);
            var pdf = await _pdfService.GeneratePdfFromHtmlAsync(html);

            var fileName = $"bilan_carbone_rse_{System.DateTime.Now:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        private string GeneratePdfHtml(StatistiquesRSE stats)
        {
            var sb = new System.Text.StringBuilder();
            var moisNom = System.DateTime.Now.ToString("MMMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
            sb.Append($@"
<!DOCTYPE html><html><head>
<meta charset='utf-8'/>
<style>
  body {{ font-family: Arial, sans-serif; color: #1a1a2e; padding: 40px; }}
  h1 {{ color: #7B5EFF; font-size: 28px; margin-bottom: 5px; }}
  h2 {{ color: #444; font-size: 16px; font-weight: normal; margin-top: 0; }}
  .kpi-grid {{ display: flex; gap: 20px; margin: 30px 0; }}
  .kpi {{ flex: 1; border: 2px solid #7B5EFF22; border-radius: 12px; padding: 16px; text-align: center; }}
  .kpi-val {{ font-size: 28px; font-weight: 900; color: #7B5EFF; }}
  .kpi-label {{ font-size: 12px; color: #666; margin-top: 4px; }}
  table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
  th {{ background: #7B5EFF; color: white; padding: 10px; text-align: left; font-size: 12px; }}
  td {{ padding: 9px 10px; border-bottom: 1px solid #eee; font-size: 12px; }}
  tr:nth-child(even) td {{ background: #f8f7ff; }}
  .footer {{ margin-top: 40px; font-size: 10px; color: #999; text-align: center; }}
  .green {{ color: #16a34a; }} .red {{ color: #dc2626; }} .orange {{ color: #d97706; }}
</style></head><body>
<h1>🌿 Bilan Carbone RSE — Flotte Véhicules</h1>
<h2>Période : {moisNom}</h2>
<div class='kpi-grid'>
  <div class='kpi'><div class='kpi-val'>{stats.TotalCO2KgCeMois:F2} kg</div><div class='kpi-label'>CO2 Total émis ce mois</div></div>
  <div class='kpi'><div class='kpi-val'>{stats.NombreTrajets}</div><div class='kpi-label'>Trajets effectués</div></div>
  <div class='kpi'><div class='kpi-val'>{stats.DistanceTotaleKm:F1} km</div><div class='kpi-label'>Distance totale</div></div>
  <div class='kpi'><div class='kpi-val green'>{stats.EconomiesPotentiellesKg:F2} kg</div><div class='kpi-label'>Économies si électrique</div></div>
</div>
<h2 style='margin-top:30px; font-size:14px; font-weight:bold; color:#333;'>Émissions par véhicule</h2>
<table><thead><tr><th>Véhicule</th><th>Carburant</th><th>g CO2/km</th><th>Trajets</th><th>Distance</th><th>CO2 Total</th></tr></thead><tbody>");
            foreach (var v in stats.CO2ParVehicule)
            {
                var co2Color = v.CO2TotalKgCeMois < 5 ? "green" : v.CO2TotalKgCeMois < 20 ? "orange" : "red";
                sb.Append($"<tr><td><strong>{v.Nom}</strong></td><td>{v.TypeCarburant}</td><td>{v.EmissionCO2ParKm:F1}</td><td>{v.NombreTrajets}</td><td>{v.DistanceTotaleKm:F1} km</td><td class='{co2Color}'><strong>{v.CO2TotalKgCeMois:F2} kg</strong></td></tr>");
            }
            sb.Append($@"</tbody></table>
<div class='footer'>Document généré par SKYRA ERP — Module Environnement RSE — {System.DateTime.Now:dd/MM/yyyy HH:mm}</div>
</body></html>");
            return sb.ToString();
        }
    }
}
