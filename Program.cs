// Fichier : Program.cs
using System.Diagnostics;
using Donnees;
using Metier;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// URL d'écoute par défaut (tu peux changer le port si tu veux)
var appUrl = "http://localhost:5000";
builder.WebHost.UseUrls(appUrl);

// Razor Pages – tes pages sont dans /Presentation/view
builder.Services
    .AddRazorPages()
    .AddRazorPagesOptions(options =>
    {
        options.RootDirectory = "/Presentation/view";
        options.Conventions.AddPageRoute("/BDDView", ""); // BDDView = page d’accueil (premier démarrage)
    });

// Service qui contient la connection string choisie via le formulaire
builder.Services.AddSingleton<DynamicConnectionProvider>();

// Service pour charger / sauvegarder la config ERP (fichier JSON)
builder.Services.AddSingleton<ErpConfigStorage>();

// *** CHARGER LA CONFIG AU DEMARRAGE ***
var tempProvider = builder.Services.BuildServiceProvider();
var configStorage = tempProvider.GetRequiredService<ErpConfigStorage>();
var savedConfig = configStorage.Load();

var dynamicProvider = tempProvider.GetRequiredService<DynamicConnectionProvider>();
dynamicProvider.CurrentConnectionString = savedConfig.ConnectionString;

// DbContext PostgreSQL (ERP) – connection string fournie dynamiquement
builder.Services.AddDbContext<ErpDbContext>((sp, options) =>
{
    var provider = sp.GetRequiredService<DynamicConnectionProvider>();

    var conn = provider.CurrentConnectionString;

    if (string.IsNullOrWhiteSpace(conn))
    {
        // Au tout premier démarrage (pas encore de BDD ERP),
        // on met une connexion neutre qui ne sera pas vraiment utilisée.
        conn = "Host=localhost;Port=5432;Database=postgres;Username=openpg;Password=faux";
    }

    options.UseNpgsql(conn);
});

// Service métier qui crée la base ERP (via le formulaire)
builder.Services.AddScoped<BDDService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

// ---------- Lancer le navigateur automatiquement ----------
OpenBrowser(appUrl);

app.Run();

// Fonction utilitaire
static void OpenBrowser(string url)
{
    try
    {
        // Windows / Linux / macOS
        var psi = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
    catch
    {
        // Si pour une raison quelconque le lancement échoue,
        // on n'empêche pas le serveur de démarrer.
    }
}
