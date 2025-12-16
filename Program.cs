// Fichier : Program.cs
using Donnees;
using Metier;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages – tes pages sont dans /Presentation/view
builder.Services
    .AddRazorPages()
    .AddRazorPagesOptions(options =>
    {
        options.RootDirectory = "/Presentation/view";
        options.Conventions.AddPageRoute("/BDDView", ""); // BDDView = page d’accueil
    });

// Service qui contient la connection string choisie via le formulaire
builder.Services.AddSingleton<DynamicConnectionProvider>();

// DbContext PostgreSQL (ERP) – connection string fournie dynamiquement
builder.Services.AddDbContext<ErpDbContext>((sp, options) =>
{
    var provider = sp.GetRequiredService<DynamicConnectionProvider>();

    // Ici, si l'utilisateur n'a pas encore passé le formulaire,
    // CurrentConnectionString sera vide => DbContext ne doit PAS être utilisé.
    var conn = provider.CurrentConnectionString;

    if (string.IsNullOrWhiteSpace(conn))
    {
        // On met quand même quelque chose pour éviter un crash au démarrage,
        // mais on ne fera aucun accès réel à la BDD tant que le formulaire
        // n'a pas été validé.
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

app.Run();
