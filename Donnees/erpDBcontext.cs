// Fichier : Donnees/erpDBcontext.cs
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Login unique
            modelBuilder.Entity<ErpUser>()
                .HasIndex(u => u.Login)
                .IsUnique();
        }
    }
}
