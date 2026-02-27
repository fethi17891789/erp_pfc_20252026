// Fichier : Metier/PdfService.cs
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using WkHtmlToPdfDotNet;
using WkHtmlToPdfDotNet.Contracts;

namespace Metier
{
    /// <summary>
    /// Service générique pour générer des PDF à partir de HTML ou de vues Razor.
    /// Réutilisable dans tous les modules (OF, factures, commandes, etc.).
    /// </summary>
    public interface IPdfService
    {
        Task<byte[]> GeneratePdfFromHtmlAsync(string html);
        Task<byte[]> GeneratePdfFromViewAsync<TModel>(string viewName, TModel model);
    }

    public class PdfService : IPdfService
    {
        private readonly IConverter _converter;
        private readonly IRazorViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PdfService(
            IConverter converter,
            IRazorViewEngine viewEngine,
            ITempDataProvider tempDataProvider,
            IServiceProvider serviceProvider,
            IHttpContextAccessor httpContextAccessor)
        {
            _converter = converter;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<byte[]> GeneratePdfFromHtmlAsync(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                throw new ArgumentException("HTML vide.", nameof(html));

            var doc = new HtmlToPdfDocument
            {
                GlobalSettings = new GlobalSettings
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 }
                },
                Objects =
                {
                    new ObjectSettings
                    {
                        HtmlContent = html,
                        WebSettings = new WebSettings
                        {
                            DefaultEncoding = "utf-8"
                        }
                    }
                }
            };

            var pdfBytes = _converter.Convert(doc);
            return Task.FromResult(pdfBytes);
        }

        public async Task<byte[]> GeneratePdfFromViewAsync<TModel>(string viewName, TModel model)
        {
            if (string.IsNullOrWhiteSpace(viewName))
                throw new ArgumentException("Nom de vue invalide.", nameof(viewName));

            var html = await RenderViewToStringAsync(viewName, model);
            return await GeneratePdfFromHtmlAsync(html);
        }

        private async Task<string> RenderViewToStringAsync<TModel>(string viewName, TModel model)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                var defaultContext = new DefaultHttpContext
                {
                    RequestServices = _serviceProvider
                };
                httpContext = defaultContext;
            }

            var actionContext = new ActionContext(
                httpContext,
                new Microsoft.AspNetCore.Routing.RouteData(),
                new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

            using var sw = new StringWriter();

            // 1) On tente avec le nom logique (comme pour une vue classique)
            var viewResult = _viewEngine.FindView(actionContext, viewName, isMainPage: false);

            // 2) Si échec, on essaie un chemin physique sous /Pages
            if (viewResult.View == null)
            {
                // Exemple : "MRP/OFTemplate" -> "/Pages/MRP/OFTemplate.cshtml"
                var physicalPath = viewName.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
                    ? viewName
                    : $"/Pages/{viewName}.cshtml";

                viewResult = _viewEngine.GetView(
                    executingFilePath: null,
                    viewPath: physicalPath,
                    isMainPage: false);
            }

            if (viewResult.View == null)
            {
                throw new InvalidOperationException($"Vue '{viewName}' introuvable.");
            }

            var viewDictionary = new ViewDataDictionary<TModel>(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary())
            {
                Model = model
            };

            var tempData = new TempDataDictionary(actionContext.HttpContext, _tempDataProvider);

            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewDictionary,
                tempData,
                sw,
                new HtmlHelperOptions());

            await viewResult.View.RenderAsync(viewContext);
            return sw.ToString();
        }
    }
}
