// Fichier : Donnees/ErpDbContext.cs
using erp_pfc_20252026.Data.Entities;
using Donnees.Logistique;
using Microsoft.EntityFrameworkCore;

namespace Donnees
{
    public class ErpDbContext : DbContext
    {
        public ErpDbContext(DbContextOptions<ErpDbContext> options)
            : base(options)
        {
        }

        public DbSet<ErpUser> ErpUsers { get; set; }
        public DbSet<Produit> Produits { get; set; }

        // BOM
        public DbSet<Bom> Boms { get; set; }
        public DbSet<BomLigne> BomLignes { get; set; }

        // MESSAGERIE
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<MessageAttachment> MessageAttachments { get; set; }
        public DbSet<MessageReadState> MessageReadStates { get; set; }

        // MRP CONFIG
        public DbSet<MRPConfigModule> MRPConfigModules { get; set; }

        // MRP PLAN
        public DbSet<MRPPlan> MRPPlans { get; set; }
        public DbSet<MRPPlanLigne> MRPPlanLignes { get; set; }

        // Détail périodique du tableau MRP
        public DbSet<MRPTableau> MRPTables { get; set; }   // ou MRPTableaux si tu préfères

        // Fichiers PDF MRP (OF) stockés en base
        public DbSet<MRPFichier> MRPFichiers { get; set; }

        // LOGISTIQUE
        public DbSet<Vehicule> LogistiqueVehicules { get; set; }
        public DbSet<Capteur> LogistiqueCapteurs { get; set; }
        public DbSet<Trajet> LogistiqueTrajets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- CONFIG USER ---
            modelBuilder.Entity<ErpUser>()
                .HasIndex(u => u.Login)
                .IsUnique();

            // --- CONFIG PRODUITS ---
            modelBuilder.Entity<Produit>(entity =>
            {
                entity.ToTable("Produits");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Nom).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Reference).HasMaxLength(50);
                entity.Property(p => p.CodeBarres).HasMaxLength(50);
                entity.Property(p => p.Type).IsRequired().HasMaxLength(20).HasDefaultValue("Bien");
                entity.Property(p => p.PrixVente).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                entity.Property(p => p.Cout).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                entity.Property(p => p.DisponibleVente).IsRequired().HasDefaultValue(true);
                entity.Property(p => p.SuiviInventaire).IsRequired().HasDefaultValue(true);
                entity.Property(p => p.Image).HasMaxLength(255);
                entity.Property(p => p.Notes).HasMaxLength(500);
                entity.Property(p => p.DateCreation)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");

                entity.HasIndex(p => p.Reference)
                      .IsUnique()
                      .HasDatabaseName("UQ_Produits_Reference");
            });

            // --- CONFIG BOM ---
            modelBuilder.Entity<Bom>(entity =>
            {
                entity.ToTable("Boms");
                entity.HasKey(b => b.Id);

                entity.HasOne(b => b.Produit)
                    .WithMany()
                    .HasForeignKey(b => b.ProduitId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(b => b.Lignes)
                    .WithOne(l => l.Bom)
                    .HasForeignKey(l => l.BomId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(b => b.ProduitId)
                      .HasDatabaseName("IX_Boms_ProduitId");
            });

            modelBuilder.Entity<BomLigne>(entity =>
            {
                entity.ToTable("BomLignes");
                entity.HasKey(l => l.Id);

                entity.Property(l => l.Quantite).HasColumnType("decimal(18,4)");
                entity.Property(l => l.PrixUnitaire).HasColumnType("decimal(18,2)");

                entity.HasOne(l => l.ComposantProduit)
                    .WithMany()
                    .HasForeignKey(l => l.ComposantProduitId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.BomId)
                      .HasDatabaseName("IX_BomLignes_BomId");
                entity.HasIndex(l => l.ComposantProduitId)
                      .HasDatabaseName("IX_BomLignes_ComposantProduitId");
            });

            // --- CONFIG MESSAGERIE ---
            modelBuilder.Entity<Conversation>(entity =>
            {
                entity.ToTable("Conversations");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Type).HasDefaultValue("direct");
                entity.Property(c => c.DateCreation).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<Message>(entity =>
            {
                entity.ToTable("Messages");
                entity.HasKey(m => m.Id);

                entity.HasOne(m => m.Conversation)
                      .WithMany(c => c.Messages)
                      .HasForeignKey(m => m.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<MessageAttachment>(entity =>
            {
                entity.ToTable("MessageAttachments");
                entity.HasKey(a => a.Id);

                entity.HasOne(a => a.Message)
                      .WithMany(m => m.Attachments)
                      .HasForeignKey(a => a.MessageId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<MessageReadState>(entity =>
            {
                entity.ToTable("MessageReadStates");
                entity.HasKey(r => r.Id);
            });

            // --- CONFIG MRP CONFIG MODULE ---
            modelBuilder.Entity<MRPConfigModule>(entity =>
            {
                entity.ToTable("MRPConfigModules");

                entity.HasKey(c => c.IdConfig);

                entity.Property(c => c.IdConfig)
                      .HasColumnName("IdConfig");

                entity.Property(c => c.HorizonParDefautJours)
                      .IsRequired();

                entity.Property(c => c.DateCreation)
                      .HasColumnType("timestamp with time zone")
                      .IsRequired();

                entity.Property(c => c.DateDerniereModification)
                      .HasColumnType("timestamp with time zone")
                      .IsRequired();

                entity.Property(c => c.CreeParUserId);
                entity.Property(c => c.ModifieParUserId);
            });

            // --- CONFIG MRP PLAN / LIGNES ---
            modelBuilder.Entity<MRPPlan>(entity =>
            {
                entity.ToTable("MRPPlans");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Reference)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(p => p.DateCreation)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");

                entity.Property(p => p.DateDebutHorizon)
                      .HasColumnType("timestamp with time zone");

                entity.Property(p => p.DateFinHorizon)
                      .HasColumnType("timestamp with time zone");

                entity.Property(p => p.HorizonJours)
                      .IsRequired();

                entity.Property(p => p.Statut)
                      .IsRequired()
                      .HasMaxLength(30)
                      .HasDefaultValue("Brouillon");

                entity.Property(p => p.TypePlan)
                      .HasMaxLength(30);

                entity.Property(p => p.Commentaire)
                      .HasMaxLength(500);

                entity.HasIndex(p => p.Reference)
                      .IsUnique()
                      .HasDatabaseName("UQ_MRPPlans_Reference");
            });

            modelBuilder.Entity<MRPPlanLigne>(entity =>
            {
                entity.ToTable("MRPPlanLignes");
                entity.HasKey(l => l.Id);

                entity.Property(l => l.TypeProduit)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.Property(l => l.DateBesoin)
                      .HasColumnType("timestamp with time zone");

                entity.Property(l => l.QuantiteBesoin)
                      .HasColumnType("decimal(18,2)");

                entity.Property(l => l.StockDisponible)
                      .HasColumnType("decimal(18,2)");

                entity.Property(l => l.QuantiteALancer)
                      .HasColumnType("decimal(18,2)");

                // Nouveau : configuration de la colonne PrixTotal
                entity.Property(l => l.PrixTotal)
                      .HasColumnType("decimal(18,2)")
                      .HasDefaultValue(0m);

                entity.HasOne(l => l.MRPPlan)
                      .WithMany(p => p.Lignes)
                      .HasForeignKey(l => l.MRPPlanId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.Produit)
                      .WithMany()
                      .HasForeignKey(l => l.ProduitId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.MRPPlanId)
                      .HasDatabaseName("IX_MRPPlanLignes_MRPPlanId");

                entity.HasIndex(l => l.ProduitId)
                      .HasDatabaseName("IX_MRPPlanLignes_ProduitId");
            });

            // --- CONFIG MRP TABLEAU (détail périodique) ---
            modelBuilder.Entity<MRPTableau>(entity =>
            {
                entity.ToTable("MRPTableaux");
                entity.HasKey(t => t.Id);

                entity.Property(t => t.NumeroPeriode)
                      .IsRequired();

                entity.Property(t => t.DatePeriode)
                      .HasColumnType("timestamp with time zone");

                entity.Property(t => t.BesoinBrut)
                      .HasColumnType("decimal(18,2)");

                entity.Property(t => t.StockPrevisionnel)
                      .HasColumnType("decimal(18,2)");

                entity.Property(t => t.BesoinNet)
                      .HasColumnType("decimal(18,2)");

                entity.Property(t => t.FinOrdre)
                      .HasColumnType("decimal(18,2)");

                entity.Property(t => t.DebutOrdre)
                      .HasColumnType("decimal(18,2)");

                entity.Property(t => t.DelaiJours)
                      .IsRequired();

                entity.HasOne(t => t.MRPPlanLigne)
                      .WithMany()
                      .HasForeignKey(t => t.MRPPlanLigneId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(t => t.MRPPlanLigneId)
                      .HasDatabaseName("IX_MRPTableaux_MRPPlanLigneId");
            });

            // --- CONFIG MRP FICHIERS (PDF OF) ---
            modelBuilder.Entity<MRPFichier>(entity =>
            {
                entity.ToTable("MRPFichiers");
                entity.HasKey(f => f.Id);

                entity.Property(f => f.CodeArticle)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(f => f.ReferenceOF)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(f => f.DateOrdre)
                      .HasColumnType("timestamp with time zone")
                      .IsRequired();

                entity.Property(f => f.FichierNom)
                      .IsRequired()
                      .HasMaxLength(255);

                entity.Property(f => f.ContentType)
                      .IsRequired()
                      .HasMaxLength(100)
                      .HasDefaultValue("application/pdf");

                entity.Property(f => f.TailleOctets)
                      .IsRequired();

                entity.Property(f => f.CreeLe)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");

                entity.Property(f => f.FichierBlob)
                      .IsRequired();

                entity.HasOne<MRPPlan>()
                      .WithMany()
                      .HasForeignKey(f => f.PlanificationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(f => f.PlanificationId)
                      .HasDatabaseName("IX_MRPFichiers_PlanificationId");
            });

            // --- CONFIG LOGISTIQUE ---
            modelBuilder.Entity<Vehicule>(entity =>
            {
                entity.ToTable("LogistiqueVehicules");
                entity.HasKey(v => v.Id);
                entity.Property(v => v.Nom).IsRequired().HasMaxLength(100);
                entity.Property(v => v.Matricule).HasMaxLength(50);
                entity.Property(v => v.TypeTransport).IsRequired().HasMaxLength(50);
            });

            modelBuilder.Entity<Capteur>(entity =>
            {
                entity.ToTable("LogistiqueCapteurs");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.IdentifiantUnique).IsRequired().HasMaxLength(100);
                entity.HasIndex(c => c.IdentifiantUnique).IsUnique();
            });

            modelBuilder.Entity<Trajet>(entity =>
            {
                entity.ToTable("LogistiqueTrajets");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Origine).HasMaxLength(255);
                entity.Property(t => t.Destination).HasMaxLength(255);
            });
        }
    }
}
