using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace erp_pfc_20252026.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        public string? RequestId { get; set; }
        public int StatusCode { get; set; }

        public void OnGet(int? statusCode = null)
        {
            RequestId = HttpContext.TraceIdentifier;
            StatusCode = statusCode ?? HttpContext.Response.StatusCode;

            var feature = HttpContext.Features.Get<IExceptionHandlerFeature>();
            if (feature?.Error != null)
            {
                Console.WriteLine($"[ERROR] Exception non gérée : {feature.Error}");
            }
        }
    }
}
