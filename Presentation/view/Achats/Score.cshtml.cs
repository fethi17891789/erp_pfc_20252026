// Fichier : Presentation/view/Achats/Score.cshtml.cs
using System.Threading.Tasks;
using Metier.Achats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages.Achats
{
    /// <summary>
    /// Endpoint JSON minimal pour récupérer le score d'un fournisseur.
    /// Appelé en AJAX depuis AnalysePrix.cshtml.
    /// Route : /Achats/Score?fournisseurId=X
    /// </summary>
    public class ScoreModel : PageModel
    {
        private readonly AchatsPrixService _prixService;

        public ScoreModel(AchatsPrixService prixService)
        {
            _prixService = prixService;
        }

        public async Task<IActionResult> OnGetAsync(int fournisseurId)
        {
            var score = await _prixService.CalculerScoreAsync(fournisseurId);
            return new JsonResult(new
            {
                score.FournisseurId,
                score.Score,
                score.Niveau,
                score.ScoreDelais,
                score.ScoreStabilite,
                score.ScoreQualite
            }, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        }
    }
}
