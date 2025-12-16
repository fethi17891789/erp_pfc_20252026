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

    // ICI : on n'utilise PLUS DefaultConnection.
    // Si la connection string n'est pas encore définie,
    // on met une valeur vide : EF ne sera vraiment utilisable
    // qu'après passage par le formulaire.
    var conn = provider.CurrentConnectionString;

    // Pour éviter une exception au tout premier démarrage,
    // on met une fausse base minimale si conn est vide,
    // mais on NE fera pas EnsureCreated tant que conn est vide.
    if (string.IsNullOrWhiteSpace(conn))
    {
        // petite base temporaire en mémoire ou nom bidon
        conn = "Host=localhost;Port=5432;Database=postgres;Username=openpg;Password=faux";
    }

    options.UseNpgsql(conn);
});

// Service métier qui crée la base ERP (via le formulaire)
builder.Services.AddScoped<BDDService>();

var app = builder.Build();

// *** BLOC MODIFIÉ : créer les tables UNIQUEMENT
// quand une vraie connection string ERP a été définie ***
using (var scope = app.Services.CreateScope())
{
    var provider = scope.ServiceProvider.GetRequiredService<DynamicConnectionProvider>();

    // Si CurrentConnectionString est vide ici,
    // cela veut dire que l'utilisateur n'a pas encore passé le formulaire.
    // Donc on NE crée PAS de tables.
    if (!string.IsNullOrWhiteSpace(provider.CurrentConnectionString))
    {
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.Database.EnsureCreated();   // crée les tables dans la base ERP choisie
    }
}
// *** FIN DU BLOC MODIFIÉ ***

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
//testfff
app.Run();