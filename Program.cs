// Fichier : Program.cs
using Donnees;
using Metier;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Diagnostics;
using WkHtmlToPdfDotNet;
using WkHtmlToPdfDotNet.Contracts;

var builder = WebApplication.CreateBuilder(args);

// URL d'écoute : Lecture depuis appsettings.json ou appsettings.Production.json
// Si non trouvé, repli sur http://0.0.0.0:5000
var appUrl = builder.Configuration["Urls"] ?? "http://0.0.0.0:5000";
builder.WebHost.UseUrls(appUrl);

// Razor Pages – tes pages sont dans /Presentation/view
builder.Services
    .AddRazorPages()
    .AddRazorPagesOptions(options =>
    {
        options.RootDirectory = "/Presentation/view";
        options.Conventions.AddPageRoute("/BDDView", "");
    });

// === wkhtmltopdf / PDF ===
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
builder.Services.AddScoped<IPdfService, PdfService>();
// ==========================

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
        conn = "Host=localhost;Port=5432;Database=fethifethifethi;Username=openpg;Password=openpgpwd";
    }

    options.UseNpgsql(conn);
});

// Service métier (Global)
builder.Services.AddScoped<BDDService>();

// Messagerie
builder.Services.AddScoped<Metier.Messagerie.MessagerieService>();

// MRP
builder.Services.AddScoped<Metier.MRP.MRPConfigService>();
builder.Services.AddScoped<Metier.MRP.OrdreFabricationService>();
builder.Services.AddScoped<Metier.MRP.OrdreAchatService>();
builder.Services.AddScoped<Metier.Logistique.LogistiqueService>();

// IA
builder.Services.AddHttpClient<Metier.IAService>();
builder.Services.AddScoped<Metier.IAService>();

// CRM
builder.Services.AddScoped<Metier.CRM.ValidationService>();
builder.Services.AddScoped<Metier.CRM.AnnuaireService>();

// BLOCKCHAIN
builder.Services.AddScoped<Metier.BlockchainService>();

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
    
    // Application du fallback universel si la configuration issue du bootstrapper est absente
    if (string.IsNullOrWhiteSpace(savedConfig.ConnectionString))
    {
        savedConfig.ConnectionString = "Host=localhost;Port=5432;Database=fethifethifethi;Username=openpg;Password=openpgpwd";
        configStorage.Save(savedConfig); // Crée le fichier erpconfig.json physiquement
        Console.WriteLine("[DEBUG] Program - Configuration de secours sauvegardée dans erpconfig.json");
    }
        
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
                const string alterSql = @"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Produits' AND column_name = 'QuantiteDisponible'
    ) THEN
        ALTER TABLE ""Produits"" ADD COLUMN ""QuantiteDisponible"" NUMERIC(18,2) NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Produits' AND column_name = 'TypeTechnique'
    ) THEN
        ALTER TABLE ""Produits"" ADD COLUMN ""TypeTechnique"" INT NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Produits' AND column_name = 'CoutAchat'
    ) THEN
        ALTER TABLE ""Produits"" ADD COLUMN ""CoutAchat"" NUMERIC(18,2) NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Produits' AND column_name = 'CoutAutresCharges'
    ) THEN
        ALTER TABLE ""Produits"" ADD COLUMN ""CoutAutresCharges"" NUMERIC(18,2) NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Produits' AND column_name = 'CoutBom'
    ) THEN
        ALTER TABLE ""Produits"" ADD COLUMN ""CoutBom"" NUMERIC(18,2) NOT NULL DEFAULT 0;
    END IF;

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
                CREATE TABLE IF NOT EXISTS ""Conversations"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Titre"" VARCHAR(200) NULL,
                    ""Type"" VARCHAR(20) NOT NULL DEFAULT 'direct',
                    ""CreatedByUserId"" INT NULL,
                    ""IsArchived"" BOOLEAN NOT NULL DEFAULT FALSE,
                    ""DateCreation"" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
                );

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

                CREATE TABLE IF NOT EXISTS ""MessageReadStates"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""MessageId"" INT NOT NULL,
                    ""UserId"" INT NOT NULL,
                    ""ReadAt"" TIMESTAMP WITHOUT TIME ZONE NOT NULL,
                    CONSTRAINT ""FK_ReadStates_Messages_MessageId""
                        FOREIGN KEY (""MessageId"") REFERENCES ""Messages""(""Id"")
                        ON DELETE CASCADE
                );";

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

// ---------- 5. CREATION AUTOMATIQUE TABLES MRPPLAN / MRPPLANLIGNES ----------
using (var scope5 = app.Services.CreateScope())
{
    try
    {
        var provider = scope5.ServiceProvider.GetRequiredService<DynamicConnectionProvider>();
        var connString = provider.CurrentConnectionString;

        Console.WriteLine($"[DEBUG] Connexion utilisée pour MRPPlans : {connString}");

        if (!string.IsNullOrWhiteSpace(connString))
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();

            const string checkMrpPlansSql = @"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'MRPPlans'
                );";

            bool mrpPlansExists;
            using (var checkCmd = new NpgsqlCommand(checkMrpPlansSql, conn))
            {
                var existsObj = checkCmd.ExecuteScalar();
                mrpPlansExists = existsObj is bool b && b;
            }

            Console.WriteLine($"[DEBUG] Table MRPPlans existe déjà ? {mrpPlansExists}");

            if (!mrpPlansExists)
            {
                const string createMrpPlansSql = @"
                    CREATE TABLE ""MRPPlans"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Reference"" VARCHAR(50) NOT NULL,
                        ""DateCreation"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
                        ""DateDebutHorizon"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""DateFinHorizon"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""HorizonJours"" INT NOT NULL,
                        ""Statut"" VARCHAR(30) NOT NULL DEFAULT 'Brouillon',
                        ""TypePlan"" VARCHAR(30),
                        ""Commentaire"" VARCHAR(500),
                        CONSTRAINT ""UQ_MRPPlans_Reference"" UNIQUE (""Reference"")
                    );";

                Console.WriteLine("[DEBUG] Création de la table MRPPlans...");
                using var createTableCmd = new NpgsqlCommand(createMrpPlansSql, conn);
                createTableCmd.ExecuteNonQuery();
                Console.WriteLine("[DEBUG] Table MRPPlans créée.");
            }

            const string checkMrpPlanLignesSql = @"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'MRPPlanLignes'
                );";

            bool mrpPlanLignesExists;
            using (var checkCmd = new NpgsqlCommand(checkMrpPlanLignesSql, conn))
            {
                var existsObj = checkCmd.ExecuteScalar();
                mrpPlanLignesExists = existsObj is bool b && b;
            }

            Console.WriteLine($"[DEBUG] Table MRPPlanLignes existe déjà ? {mrpPlanLignesExists}");

            if (!mrpPlanLignesExists)
            {
                const string createMrpPlanLignesSql = @"
                    CREATE TABLE ""MRPPlanLignes"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""MRPPlanId"" INT NOT NULL,
                        ""ProduitId"" INT NOT NULL,
                        ""TypeProduit"" VARCHAR(20) NOT NULL,
                        ""DateBesoin"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""QuantiteBesoin"" NUMERIC(18,2) NOT NULL,
                        ""StockDisponible"" NUMERIC(18,2) NOT NULL,
                        ""QuantiteALancer"" NUMERIC(18,2) NOT NULL,
                        ""PrixTotal"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                        CONSTRAINT ""FK_MRPPlanLignes_MRPPlans_MRPPlanId""
                            FOREIGN KEY (""MRPPlanId"") REFERENCES ""MRPPlans""(""Id"")
                            ON DELETE CASCADE,
                        CONSTRAINT ""FK_MRPPlanLignes_Produits_ProduitId""
                            FOREIGN KEY (""ProduitId"") REFERENCES ""Produits""(""Id"")
                            ON DELETE RESTRICT
                    );";

                Console.WriteLine("[DEBUG] Création de la table MRPPlanLignes...");
                using var createTableCmd2 = new NpgsqlCommand(createMrpPlanLignesSql, conn);
                createTableCmd2.ExecuteNonQuery();
                Console.WriteLine("[DEBUG] Table MRPPlanLignes créée.");
            }
            else
            {
                const string alterMrpPlanLignesSql = @"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'MRPPlanLignes'
          AND column_name = 'PrixTotal'
    ) THEN
        ALTER TABLE ""MRPPlanLignes""
        ADD COLUMN ""PrixTotal"" NUMERIC(18,2) NOT NULL DEFAULT 0;
    END IF;
END $$;";
                using var alterCmd = new NpgsqlCommand(alterMrpPlanLignesSql, conn);
                alterCmd.ExecuteNonQuery();
                Console.WriteLine("[DEBUG] Colonne PrixTotal MRPPlanLignes OK.");
            }
        }
        else
        {
            Console.WriteLine("[DEBUG] Connexion vide, aucune création de tables MRPPlans.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de la création auto des tables MRPPlans : {ex}");
    }
}
// --------------------------------------------------------------------

// ---------- 6. CREATION AUTOMATIQUE TABLE MRP FICHIERS (PDF OF) ----------
using (var scope6 = app.Services.CreateScope())
{
    try
    {
        var provider = scope6.ServiceProvider.GetRequiredService<DynamicConnectionProvider>();
        var connString = provider.CurrentConnectionString;

        Console.WriteLine($"[DEBUG] Connexion utilisée pour MRPFichiers : {connString}");

        if (!string.IsNullOrWhiteSpace(connString))
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();

            const string checkMrpFichiersSql = @"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'MRPFichiers'
                );";

            bool mrpFichiersExists;
            using (var checkCmd = new NpgsqlCommand(checkMrpFichiersSql, conn))
            {
                var existsObj = checkCmd.ExecuteScalar();
                mrpFichiersExists = existsObj is bool b && b;
            }

            Console.WriteLine($"[DEBUG] Table MRPFichiers existe déjà ? {mrpFichiersExists}");

            if (!mrpFichiersExists)
            {
                const string createMrpFichiersSql = @"
                    CREATE TABLE ""MRPFichiers"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""PlanificationId"" INT NOT NULL,
                        ""CodeArticle"" VARCHAR(50) NOT NULL,
                        ""ReferenceOF"" VARCHAR(50) NOT NULL,
                        ""DateOrdre"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""FichierNom"" VARCHAR(255) NOT NULL,
                        ""ContentType"" VARCHAR(100) NOT NULL DEFAULT 'application/pdf',
                        ""TailleOctets"" BIGINT NOT NULL,
                        ""CreeLe"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
                        ""FichierBlob"" BYTEA NOT NULL,
                        CONSTRAINT ""FK_MRPFichiers_MRPPlans_PlanificationId""
                            FOREIGN KEY (""PlanificationId"") REFERENCES ""MRPPlans""(""Id"")
                            ON DELETE CASCADE
                    );";

                Console.WriteLine("[DEBUG] Création de la table MRPFichiers...");
                using var createTableCmd = new NpgsqlCommand(createMrpFichiersSql, conn);
                createTableCmd.ExecuteNonQuery();
                Console.WriteLine("[DEBUG] Table MRPFichiers créée.");
            }
        }
        else
        {
            Console.WriteLine("[DEBUG] Connexion vide, aucune création de table MRPFichiers.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de la création auto de la table MRPFichiers : {ex}");
    }
}
// --------------------------------------------------------------------

// ---------- 7. CREATION AUTOMATIQUE TABLES LOGISTIQUE ----------
using (var scope7 = app.Services.CreateScope())
{
    try
    {
        var provider = scope7.ServiceProvider.GetRequiredService<DynamicConnectionProvider>();
        var connString = provider.CurrentConnectionString;

        if (!string.IsNullOrWhiteSpace(connString))
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();

            const string createLogistiqueTablesSql = @"
                CREATE TABLE IF NOT EXISTS ""LogistiqueVehicules"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Nom"" VARCHAR(100) NOT NULL,
                    ""Matricule"" VARCHAR(50),
                    ""TypeTransport"" VARCHAR(50) NOT NULL,
                    ""Statut"" VARCHAR(50) NOT NULL DEFAULT 'Disponible',
                    ""Latitude"" DOUBLE PRECISION NULL,
                    ""Longitude"" DOUBLE PRECISION NULL,
                    ""DerniereMiseAJour"" TIMESTAMP WITHOUT TIME ZONE NULL,
                    ""DateCreation"" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
                    ""Marque"" VARCHAR(100) NULL,
                    ""Modele"" VARCHAR(100) NULL,
                    ""Annee"" INT NULL,
                    ""TypeCarburant"" VARCHAR(50) NULL,
                    ""EmissionCO2ParKm"" DOUBLE PRECISION NULL
                );

                CREATE TABLE IF NOT EXISTS ""LogistiqueCapteurs"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""IdentifiantUnique"" VARCHAR(100) NOT NULL,
                    ""VehiculeId"" INT NULL,
                    ""Description"" VARCHAR(255),
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                    ""DateCreation"" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
                    CONSTRAINT ""UQ_Capteurs_IdentifiantUnique"" UNIQUE (""IdentifiantUnique"")
                );

                CREATE TABLE IF NOT EXISTS ""LogistiqueTrajets"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""VehiculeId"" INT NOT NULL,
                    ""CapteurId"" INT NOT NULL,
                    ""DateDebut"" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
                    ""DateFin"" TIMESTAMP WITHOUT TIME ZONE NULL,
                    ""Origine"" VARCHAR(255),
                    ""Destination"" VARCHAR(255),
                    ""DistanceParcourueKm"" DOUBLE PRECISION NOT NULL DEFAULT 0,
                    ""Statut"" VARCHAR(50) NOT NULL DEFAULT 'En Cours',
                    ""TraceJson"" TEXT NULL,
                    ""Co2EmisGrammes"" DOUBLE PRECISION NOT NULL DEFAULT 0,
                    ""DureeArretMinutes"" DOUBLE PRECISION NOT NULL DEFAULT 0,
                    ""ItineraireType"" VARCHAR(50) NULL
                );";

            using (var cmd = new NpgsqlCommand(createLogistiqueTablesSql, conn))
            {
                Console.WriteLine("[DEBUG] Vérification / Création des tables Logistique...");
                cmd.ExecuteNonQuery();
                Console.WriteLine("[DEBUG] Tables Logistique prêtes.");
            }

            // Migration RSE : ajout des colonnes sur tables existantes (idempotent)
            var alterCols = new[]
            {
                @"ALTER TABLE ""LogistiqueVehicules"" ADD COLUMN IF NOT EXISTS ""Marque"" VARCHAR(100) NULL",
                @"ALTER TABLE ""LogistiqueVehicules"" ADD COLUMN IF NOT EXISTS ""Modele"" VARCHAR(100) NULL",
                @"ALTER TABLE ""LogistiqueVehicules"" ADD COLUMN IF NOT EXISTS ""Annee"" INT NULL",
                @"ALTER TABLE ""LogistiqueVehicules"" ADD COLUMN IF NOT EXISTS ""TypeCarburant"" VARCHAR(50) NULL",
                @"ALTER TABLE ""LogistiqueVehicules"" ADD COLUMN IF NOT EXISTS ""EmissionCO2ParKm"" DOUBLE PRECISION NULL",
                @"ALTER TABLE ""LogistiqueTrajets"" ADD COLUMN IF NOT EXISTS ""Co2EmisGrammes"" DOUBLE PRECISION NOT NULL DEFAULT 0",
                @"ALTER TABLE ""LogistiqueTrajets"" ADD COLUMN IF NOT EXISTS ""DureeArretMinutes"" DOUBLE PRECISION NOT NULL DEFAULT 0",
                @"ALTER TABLE ""LogistiqueTrajets"" ADD COLUMN IF NOT EXISTS ""ItineraireType"" VARCHAR(50) NULL",
            };
            foreach (var sql in alterCols)
            {
                try
                {
                    using var cmdAlt = new NpgsqlCommand(sql, conn);
                    cmdAlt.ExecuteNonQuery();
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"[DEBUG] ALTER (ignoré si colonne existe déjà) : {ex2.Message}");
                }
            }
            Console.WriteLine("[DEBUG] Colonnes RSE Logistique prêtes.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de la création auto des tables Logistique : {ex}");
    }
}
// --------------------------------------------------------------------

// ---------- 8. CREATION AUTOMATIQUE TABLE IA CONFIGURATION ----------
using (var scope8 = app.Services.CreateScope())
{
    try
    {
        var provider = scope8.ServiceProvider.GetRequiredService<DynamicConnectionProvider>();
        var connString = provider.CurrentConnectionString;

        Console.WriteLine($"[DEBUG] Connexion utilisée pour IaConfiguration : {connString}");

        if (!string.IsNullOrWhiteSpace(connString))
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();

            const string createIaConfigSql = @"
                CREATE TABLE IF NOT EXISTS ""IaConfiguration"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Provider"" VARCHAR(255) NOT NULL DEFAULT 'Gemini',
                    ""ApiKey"" VARCHAR(255) NULL,
                    ""SystemPrompt"" TEXT NULL,
                    ""ModelName"" VARCHAR(255) NOT NULL DEFAULT 'gemini-1.5-flash',
                    ""IsEnabled"" BOOLEAN NOT NULL DEFAULT TRUE,
                    ""DateDerniereModification"" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
                );";

            using (var cmd = new NpgsqlCommand(createIaConfigSql, conn))
            {
                Console.WriteLine("[DEBUG] Vérification / Création de la table IaConfiguration...");
                cmd.ExecuteNonQuery();
                
                // Insertion ligne par défaut si vide
                const string seedSql = @"
                    INSERT INTO ""IaConfiguration"" (""Provider"", ""ModelName"")
                    SELECT 'Gemini', 'gemini-1.5-flash'
                    WHERE NOT EXISTS (SELECT 1 FROM ""IaConfiguration"");";
                using (var seedCmd = new NpgsqlCommand(seedSql, conn))
                {
                    seedCmd.ExecuteNonQuery();
                }
                
                Console.WriteLine("[DEBUG] Table IaConfiguration prête.");
                
                // Renommage automatique de l'IA pour l'esthétique
                using (var renameCmd = new NpgsqlCommand("UPDATE \"ErpUsers\" SET \"Login\"='GEMINI' WHERE \"Login\"='skyra-ia';", conn))
                {
                    renameCmd.ExecuteNonQuery();
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de la création auto de IaConfiguration : {ex}");
    }
}
// --------------------------------------------------------------------

// ---------- 9. CREATION AUTOMATIQUE TABLES ANNUAIRE (CRM) ----------
using (var scope9 = app.Services.CreateScope())
{
    try
    {
        var provider = scope9.ServiceProvider.GetRequiredService<DynamicConnectionProvider>();
        var connString = provider.CurrentConnectionString;

        Console.WriteLine($"[DEBUG] Connexion utilisée pour CRM Annuaire : {connString}");

        if (!string.IsNullOrWhiteSpace(connString))
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();

            const string createCrmTablesSql = @"
                CREATE TABLE IF NOT EXISTS ""Contacts"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""FullName"" VARCHAR(200) NOT NULL,
                    ""Email"" VARCHAR(200) NULL,
                    ""Phone"" VARCHAR(50) NULL,
                    ""Website"" VARCHAR(255) NULL,
                    ""Roles"" INT NOT NULL DEFAULT 0,
                    ""AvatarImage"" TEXT NULL,
                    ""Comment"" VARCHAR(1000) NULL,
                    ""DateCreation"" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS ""ContactRelations"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""SourceContactId"" INT NOT NULL,
                    ""TargetContactId"" INT NOT NULL,
                    ""RelationType"" VARCHAR(100) NOT NULL,
                    CONSTRAINT ""FK_ContactRelations_Source""
                        FOREIGN KEY (""SourceContactId"") REFERENCES ""Contacts""(""Id"")
                        ON DELETE CASCADE,
                    CONSTRAINT ""FK_ContactRelations_Target""
                        FOREIGN KEY (""TargetContactId"") REFERENCES ""Contacts""(""Id"")
                        ON DELETE CASCADE
                );";

            using (var cmd = new NpgsqlCommand(createCrmTablesSql, conn))
            {
                Console.WriteLine("[DEBUG] Vérification / Création des tables CRM (Annuaire)...");
                cmd.ExecuteNonQuery();
                Console.WriteLine("[DEBUG] Tables CRM prêtes.");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de la création auto des tables CRM : {ex}");
    }
}
// --------------------------------------------------------------------


// ---------- 10. CREATION AUTOMATIQUE TABLE BLOCKCHAIN ----------
using (var scope10 = app.Services.CreateScope())
{
    try
    {
        var provider = scope10.ServiceProvider.GetRequiredService<DynamicConnectionProvider>();
        var connString = provider.CurrentConnectionString;

        if (!string.IsNullOrWhiteSpace(connString))
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();

            const string createBlockchainSql = @"
                CREATE TABLE IF NOT EXISTS ""BlockchainAncrages"" (
                    ""Id""             SERIAL PRIMARY KEY,
                    ""TypeDocument""   VARCHAR(20)  NOT NULL,
                    ""RefDocument""    VARCHAR(100) NOT NULL,
                    ""HashContenu""    VARCHAR(64)  NOT NULL,
                    ""TxHash""         VARCHAR(100) NULL,
                    ""LienEtherscan""  VARCHAR(200) NULL,
                    ""Statut""         VARCHAR(20)  NOT NULL DEFAULT 'Local',
                    ""DateAncrage""    TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
                    ""CreeParUserId""  INT NULL
                );
                CREATE INDEX IF NOT EXISTS ""IX_BlockchainAncrages_RefDocument""
                    ON ""BlockchainAncrages""(""RefDocument"");";

            using (var cmd = new NpgsqlCommand(createBlockchainSql, conn))
            {
                Console.WriteLine("[DEBUG] Vérification / Création de la table BlockchainAncrages...");
                cmd.ExecuteNonQuery();
                Console.WriteLine("[DEBUG] Table BlockchainAncrages prête.");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de la création auto de BlockchainAncrages : {ex}");
    }
}
// --------------------------------------------------------------------

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapRazorPages();

app.MapHub<Metier.Messagerie.ChatHub>("/chathub");
app.MapHub<Metier.Logistique.LogistiqueHub>("/logistiquehub");

// Ouvrir le navigateur sur l'URL configurée (mais en localhost pour le client local)
var publicUrl = appUrl.Replace("0.0.0.0", "localhost");
OpenBrowser(publicUrl);

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
