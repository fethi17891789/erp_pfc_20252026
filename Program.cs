// Fichier : Program.cs
using System;
using System.Diagnostics;
using Donnees;
using Metier;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// URL d'écoute par défaut
var appUrl = "http://localhost:5000";
builder.WebHost.UseUrls(appUrl);

// Razor Pages – tes pages sont dans /Presentation/view
builder.Services
    .AddRazorPages()
    .AddRazorPagesOptions(options =>
    {
        options.RootDirectory = "/Presentation/view";
        options.Conventions.AddPageRoute("/BDDView", "");
    });

// Services singletons
builder.Services.AddSingleton<DynamicConnectionProvider>();
builder.Services.AddSingleton<ErpConfigStorage>();

// DbContext PostgreSQL (ERP) – connection string fournie dynamiquement
builder.Services.AddDbContext<ErpDbContext>((sp, options) =>
{
    var provider = sp.GetRequiredService<DynamicConnectionProvider>();
    var conn = provider.CurrentConnectionString;

    if (string.IsNullOrWhiteSpace(conn))
    {
        // Connexion neutre tant que l'utilisateur n'a pas configuré la BDD
        conn = "Host=localhost;Port=5432;Database=postgres;Username=openpg;Password=faux";
    }

    options.UseNpgsql(conn);
});

// Service métier
builder.Services.AddScoped<BDDService>();

var app = WebApplication.CreateBuilder(args).Build(); // <-- si tu avais recopié, remplace par :
app = builder.Build();

// ********** CHARGER LA CONFIG APRES Build(), AVEC LE VRAI ServiceProvider **********
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;

    var configStorage = sp.GetRequiredService<ErpConfigStorage>();
    var savedConfig = configStorage.Load();

    Console.WriteLine($"[DEBUG] Program - ConnectionString après Load : {savedConfig.ConnectionString}");

    var dynamicProvider = sp.GetRequiredService<DynamicConnectionProvider>();
    dynamicProvider.CurrentConnectionString = savedConfig.ConnectionString;
}
// *****************************************************************************


// ---------- CREATION AUTOMATIQUE DE LA TABLE "Produits" S'IL LE FAUT ----------
using (var scope = app.Services.CreateScope())
{
    try
    {
        var provider = scope.ServiceProvider.GetRequiredService<DynamicConnectionProvider>();
        var connString = provider.CurrentConnectionString;

        Console.WriteLine($"[DEBUG] Connexion utilisée pour Produits : {connString}");

        if (!string.IsNullOrWhiteSpace(connString))
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();

            const string checkSql = @"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'Produits'
                );";

            using (var checkCmd = new NpgsqlCommand(checkSql, conn))
            {
                var existsObj = checkCmd.ExecuteScalar();
                var exists = existsObj is bool b && b;

                Console.WriteLine($"[DEBUG] Table Produits existe déjà ? {exists}");

                if (!exists)
                {
                    const string createSql = @"
                        CREATE TABLE ""Produits"" (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""Nom"" VARCHAR(100) NOT NULL,
                            ""Reference"" VARCHAR(50),
                            ""CodeBarres"" VARCHAR(50),
                            ""Type"" VARCHAR(20) NOT NULL DEFAULT 'Bien',
                            ""PrixVente"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                            ""Cout"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                            ""DisponibleVente"" BOOLEAN NOT NULL DEFAULT TRUE,
                            ""SuiviInventaire"" BOOLEAN NOT NULL DEFAULT TRUE,
                            ""Image"" VARCHAR(255),
                            ""Notes"" VARCHAR(500),
                            ""DateCreation"" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
                            CONSTRAINT ""UQ_Produits_Reference"" UNIQUE (""Reference"")
                        );";

                    Console.WriteLine("[DEBUG] Création de la table Produits...");
                    using var createCmd = new NpgsqlConnection(connString);
                    createCmd.Open();
                    using var createTableCmd = new NpgsqlCommand(createSql, createCmd);
                    createTableCmd.ExecuteNonQuery();
                    Console.WriteLine("[DEBUG] Table Produits créée.");
                }
            }
        }
        else
        {
            Console.WriteLine("[DEBUG] Connexion vide, aucune création de table Produits.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de la création auto de la table Produits : {ex}");
    }
}
// ------------------------------------------------------------------------------

// ---------- CREATION AUTOMATIQUE DES TABLES BOM / BOMLIGNES ----------
using (var scope2 = app.Services.CreateScope())
{
    try
    {
        var provider = scope2.ServiceProvider.GetRequiredService<DynamicConnectionProvider>();
        var connString = provider.CurrentConnectionString;

        Console.WriteLine($"[DEBUG] Connexion utilisée pour BOM : {connString}");

        if (!string.IsNullOrWhiteSpace(connString))
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();

            // Table Boms
            const string checkBomsSql = @"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'Boms'
                );";

            bool bomsExists;
            using (var checkCmd = new NpgsqlCommand(checkBomsSql, conn))
            {
                var existsObj = checkCmd.ExecuteScalar();
                bomsExists = existsObj is bool b && b;
            }

            Console.WriteLine($"[DEBUG] Table Boms existe déjà ? {bomsExists}");

            if (!bomsExists)
            {
                const string createBomsSql = @"
                    CREATE TABLE ""Boms"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""ProduitId"" INT NOT NULL,
                        CONSTRAINT ""FK_Boms_Produits_ProduitId""
                            FOREIGN KEY (""ProduitId"") REFERENCES ""Produits""(""Id"")
                            ON DELETE RESTRICT
                    );";

                using (var createCmd = new NpgsqlCommand(createBomsSql, conn))
                {
                    Console.WriteLine("[DEBUG] Création de la table Boms...");
                    createCmd.ExecuteNonQuery();
                    Console.WriteLine("[DEBUG] Table Boms créée.");
                }
            }

            // Table BomLignes
            const string checkBomLignesSql = @"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'BomLignes'
                );";

            bool bomLignesExists;
            using (var checkCmd = new NpgsqlCommand(checkBomLignesSql, conn))
            {
                var existsObj = checkCmd.ExecuteScalar();
                bomLignesExists = existsObj is bool b && b;
            }

            Console.WriteLine($"[DEBUG] Table BomLignes existe déjà ? {bomLignesExists}");

            if (!bomLignesExists)
            {
                const string createBomLignesSql = @"
                    CREATE TABLE ""BomLignes"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""BomId"" INT NOT NULL,
                        ""ComposantProduitId"" INT NOT NULL,
                        ""Quantite"" NUMERIC(18,4) NOT NULL DEFAULT 1,
                        ""PrixUnitaire"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                        CONSTRAINT ""FK_BomLignes_Boms_BomId""
                            FOREIGN KEY (""BomId"") REFERENCES ""Boms""(""Id"")
                            ON DELETE CASCADE,
                        CONSTRAINT ""FK_BomLignes_Produits_ComposantProduitId""
                            FOREIGN KEY (""ComposantProduitId"") REFERENCES ""Produits""(""Id"")
                            ON DELETE RESTRICT
                    );";

                using (var createCmd = new NpgsqlCommand(createBomLignesSql, conn))
                {
                    Console.WriteLine("[DEBUG] Création de la table BomLignes...");
                    createCmd.ExecuteNonQuery();
                    Console.WriteLine("[DEBUG] Table BomLignes créée.");
                }
            }
        }
        else
        {
            Console.WriteLine("[DEBUG] Connexion vide, aucune création de tables BOM.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de la création auto des tables BOM : {ex}");
    }
}
// --------------------------------------------------------------------

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

// Lancer le navigateur automatiquement
OpenBrowser(appUrl);

app.Run();

static void OpenBrowser(string url)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
    catch
    {
        // On ignore les erreurs de lancement du navigateur
    }
}
