// Fichier : Donnees/ErpDbContext.cs
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

        // AJOUT BOM
        public DbSet<Bom> Boms { get; set; }
        public DbSet<BomLigne> BomLignes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ErpUser>()
                .HasIndex(u => u.Login)
                .IsUnique();

            modelBuilder.Entity<Produit>(entity =>
            {
                entity.ToTable("Produits");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Nom)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(p => p.Reference)
                    .HasMaxLength(50);

                entity.Property(p => p.CodeBarres)
                    .HasMaxLength(50);

                entity.Property(p => p.Type)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("Bien");

                entity.Property(p => p.PrixVente)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0m);

                entity.Property(p => p.Cout)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0m);

                entity.Property(p => p.DisponibleVente)
                    .IsRequired()
                    .HasDefaultValue(true);

                entity.Property(p => p.SuiviInventaire)
                    .IsRequired()
                    .HasDefaultValue(true);

                entity.Property(p => p.Image)
                    .HasMaxLength(255);

                entity.Property(p => p.Notes)
                    .HasMaxLength(500);

                entity.Property(p => p.DateCreation)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");

                entity.HasIndex(p => p.Reference)
                    .IsUnique()
                    .HasDatabaseName("UQ_Produits_Reference");
            });

            // CONFIG BOM
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

                entity.Property(l => l.Quantite)
                    .HasColumnType("decimal(18,4)");

                entity.Property(l => l.PrixUnitaire)
                    .HasColumnType("decimal(18,2)");

                entity.HasOne(l => l.ComposantProduit)
                    .WithMany()
                    .HasForeignKey(l => l.ComposantProduitId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.BomId)
                    .HasDatabaseName("IX_BomLignes_BomId");

                entity.HasIndex(l => l.ComposantProduitId)
                    .HasDatabaseName("IX_BomLignes_ComposantProduitId");
            });
        }
    }
}
