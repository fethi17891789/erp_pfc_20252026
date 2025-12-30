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
                    .IsRequired()  // ← AJOUT
                    .HasDefaultValue(true);

                entity.Property(p => p.SuiviInventaire)
                    .IsRequired()  // ← AJOUT
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
                    .HasName("UQ_Produits_Reference");
            });
        }
    }
}
