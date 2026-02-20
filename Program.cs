// Fichier : Program.cs
using System;
using System.Diagnostics;
using Donnees;
using Metier;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Http;

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
        conn = "Host=localhost;Port=5432;Database=postgres;Username=openpg;Password=faux";
    }

    options.UseNpgsql(conn);
});

// Service métier (Global)
builder.Services.AddScoped<BDDService>();

// Messagerie
builder.Services.AddScoped<Metier.Messagerie.MessagerieService>();

// MRP
builder.Services.AddScoped<Metier.MRP.MRPConfigService>();

// SignalR
builder.Services.AddSignalR();

// SESSION
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".erp_pfc_20252026.session";
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Charger la config après Build()
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;

    var configStorage = sp.GetRequiredService<ErpConfigStorage>();
    var savedConfig = configStorage.Load();

    Console.WriteLine($"[DEBUG] Program - ConnectionString après Load : {savedConfig.ConnectionString}");

    var dynamicProvider = sp.GetRequiredService<DynamicConnectionProvider>();
    dynamicProvider.CurrentConnectionString = savedConfig.ConnectionString;
}

// ---------- 1. CREATION / MISE À JOUR TABLE "Produits" ----------
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

            bool exists;
            using (var checkCmd = new NpgsqlCommand(checkSql, conn))
            {
                var existsObj = checkCmd.ExecuteScalar();
                exists = existsObj is bool b && b;
            }

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
                        ""QuantiteDisponible"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                        ""DisponibleVente"" BOOLEAN NOT NULL DEFAULT TRUE,
                        ""SuiviInventaire"" BOOLEAN NOT NULL DEFAULT TRUE,
                        ""Image"" VARCHAR(255),
                        ""Notes"" VARCHAR(500),
                        ""DateCreation"" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
                        ""TypeTechnique"" INT NOT NULL DEFAULT 0,
                        ""CoutAchat"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                        ""CoutAutresCharges"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                        ""CoutBom"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                        ""CoutTotal"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                        CONSTRAINT ""UQ_Produits_Reference"" UNIQUE (""Reference"")
                    );";

                Console.WriteLine("[DEBUG] Création de la table Produits...");
                using var createTableCmd = new NpgsqlCommand(createSql, conn);
                createTableCmd.ExecuteNonQuery();
                Console.WriteLine("[DEBUG] Table Produits créée.");
            }
            else
            {
                // ALTER TABLE pour ajouter les colonnes manquantes
                const string alterSql = @"
DO $$
BEGIN
    -- QuantiteDisponible
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Produits' AND column_name = 'QuantiteDisponible'
    ) THEN
        ALTER TABLE ""Produits"" ADD COLUMN ""QuantiteDisponible"" NUMERIC(18,2) NOT NULL DEFAULT 0;
    END IF;

    -- TypeTechnique
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Produits' AND column_name = 'TypeTechnique'
    ) THEN
        ALTER TABLE ""Produits"" ADD COLUMN ""TypeTechnique"" INT NOT NULL DEFAULT 0;
    END IF;

    -- CoutAchat
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Produits' AND column_name = 'CoutAchat'
    ) THEN
        ALTER TABLE ""Produits"" ADD COLUMN ""CoutAchat"" NUMERIC(18,2) NOT NULL DEFAULT 0;
    END IF;

    -- CoutAutresCharges
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Produits' AND column_name = 'CoutAutresCharges'
    ) THEN
        ALTER TABLE ""Produits"" ADD COLUMN ""CoutAutresCharges"" NUMERIC(18,2) NOT NULL DEFAULT 0;
    END IF;

    -- CoutBom
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Produits' AND column_name = 'CoutBom'
    ) THEN
        ALTER TABLE ""Produits"" ADD COLUMN ""CoutBom"" NUMERIC(18,2) NOT NULL DEFAULT 0;
    END IF;

    -- CoutTotal
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Produits' AND column_name = 'CoutTotal'
    ) THEN
        ALTER TABLE ""Produits"" ADD COLUMN ""CoutTotal"" NUMERIC(18,2) NOT NULL DEFAULT 0;
    END IF;
END $$;";

                Console.WriteLine("[DEBUG] ALTER TABLE Produits pour colonnes de coût...");
                using var alterCmd = new NpgsqlCommand(alterSql, conn);
                alterCmd.ExecuteNonQuery();
                Console.WriteLine("[DEBUG] Colonnes de coût Produits OK.");
            }
        }
        else
        {
            Console.WriteLine("[DEBUG] Connexion vide, aucune création de table Produits.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de la création/MAJ auto de la table Produits : {ex}");
    }
}
// ------------------------------------------------------------------------------

// ---------- 2. CREATION AUTOMATIQUE DES TABLES BOM / BOMLIGNES ----------
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
                        ""AutresCharges"" NUMERIC(18,2) NOT NULL DEFAULT 0,
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
            else
            {
                // Ajout de la colonne AutresCharges si elle manque
                const string alterBomLignesSql = @"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'BomLignes'
          AND column_name = 'AutresCharges'
    ) THEN
        ALTER TABLE ""BomLignes""
        ADD COLUMN ""AutresCharges"" NUMERIC(18,2) NOT NULL DEFAULT 0;
    END IF;
END $$;";
                using var alterBomLignesCmd = new NpgsqlCommand(alterBomLignesSql, conn);
                alterBomLignesCmd.ExecuteNonQuery();
                Console.WriteLine("[DEBUG] Colonne AutresCharges BomLignes OK.");
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

// ---------- 3. CREATION AUTOMATIQUE DES TABLES DE MESSAGERIE ----------
using (var scope3 = app.Services.CreateScope())
{
    try
    {
        var provider = scope3.ServiceProvider.GetRequiredService<DynamicConnectionProvider>();
        var connString = provider.CurrentConnectionString;

        Console.WriteLine($"[DEBUG] Connexion utilisée pour MESSAGERIE : {connString}");

        if (!string.IsNullOrWhiteSpace(connString))
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();

            const string createMessagingTablesSql = @"
                -- 1. Table Conversations
                CREATE TABLE IF NOT EXISTS ""Conversations"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Titre"" VARCHAR(200) NULL,
                    ""Type"" VARCHAR(20) NOT NULL DEFAULT 'direct',
                    ""CreatedByUserId"" INT NULL,
                    ""IsArchived"" BOOLEAN NOT NULL DEFAULT FALSE,
                    ""DateCreation"" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
                );

                -- 2. Table Messages
                CREATE TABLE IF NOT EXISTS ""Messages"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""ConversationId"" INT NOT NULL,
                    ""SenderId"" INT NOT NULL,
                    ""Content"" TEXT NULL,
                    ""MessageType"" VARCHAR(20) NOT NULL DEFAULT 'text',
                    ""IsEdited"" BOOLEAN NOT NULL DEFAULT FALSE,
                    ""EditedAt"" TIMESTAMP WITHOUT TIME ZONE NULL,
                    ""IsDeleted"" BOOLEAN NOT NULL DEFAULT FALSE,
                    ""Timestamp"" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
                    CONSTRAINT ""FK_Messages_Conversations_ConversationId""
                        FOREIGN KEY (""ConversationId"") REFERENCES ""Conversations""(""Id"")
                        ON DELETE CASCADE
                );

                -- 3. Table MessageAttachments
                CREATE TABLE IF NOT EXISTS ""MessageAttachments"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""MessageId"" INT NOT NULL,
                    ""AttachmentType"" VARCHAR(20) NOT NULL,
                    ""FileName"" VARCHAR(255) NOT NULL,
                    ""FileUrl"" VARCHAR(500) NOT NULL,
                    ""FileSizeBytes"" BIGINT NULL,
                    CONSTRAINT ""FK_Attachments_Messages_MessageId""
                        FOREIGN KEY (""MessageId"") REFERENCES ""Messages""(""Id"")
                        ON DELETE CASCADE
                );

                -- 4. Table MessageReadStates
                CREATE TABLE IF NOT EXISTS ""MessageReadStates"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""MessageId"" INT NOT NULL,
                    ""UserId"" INT NOT NULL,
                    ""ReadAt"" TIMESTAMP WITHOUT TIME ZONE NOT NULL,
                    CONSTRAINT ""FK_ReadStates_Messages_MessageId""
                        FOREIGN KEY (""MessageId"") REFERENCES ""Messages""(""Id"")
                        ON DELETE CASCADE
                );
            ";

            using (var cmd = new NpgsqlCommand(createMessagingTablesSql, conn))
            {
                Console.WriteLine("[DEBUG] Vérification / Création des tables Messagerie...");
                cmd.ExecuteNonQuery();
                Console.WriteLine("[DEBUG] Tables Messagerie prêtes.");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de la création auto des tables Messagerie : {ex}");
    }
}
// --------------------------------------------------------------------

// ---------- 4. CREATION AUTOMATIQUE TABLE MRP CONFIG MODULE ----------
using (var scope4 = app.Services.CreateScope())
{
    try
    {
        var provider = scope4.ServiceProvider.GetRequiredService<DynamicConnectionProvider>();
        var connString = provider.CurrentConnectionString;

        Console.WriteLine($"[DEBUG] Connexion utilisée pour MRPConfigModules : {connString}");

        if (!string.IsNullOrWhiteSpace(connString))
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();

            const string createMrpConfigTableSql = @"
                CREATE TABLE IF NOT EXISTS ""MRPConfigModules"" (
                    ""IdConfig""                SERIAL PRIMARY KEY,
                    ""HorizonParDefautJours""   INT NOT NULL,
                    ""DateCreation""            TIMESTAMP WITHOUT TIME ZONE NOT NULL,
                    ""DateDerniereModification"" TIMESTAMP WITHOUT TIME ZONE NOT NULL,
                    ""CreeParUserId""           INT NULL,
                    ""ModifieParUserId""        INT NULL
                );";

            using (var cmd = new NpgsqlCommand(createMrpConfigTableSql, conn))
            {
                Console.WriteLine("[DEBUG] Vérification / Création de la table MRPConfigModules...");
                cmd.ExecuteNonQuery();
                Console.WriteLine("[DEBUG] Table MRPConfigModules prête.");
            }
        }
        else
        {
            Console.WriteLine("[DEBUG] Connexion vide, aucune création de table MRPConfigModules.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de la création auto de la table MRPConfigModules : {ex}");
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

app.UseSession();

app.UseAuthorization();

app.MapRazorPages();

app.MapHub<Metier.Messagerie.ChatHub>("/chathub");

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
    }
}
