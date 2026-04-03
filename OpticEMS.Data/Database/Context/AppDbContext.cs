using Microsoft.EntityFrameworkCore;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Data.Database.Configurations;

namespace OpticEMS.Data.Database.Context
{
    public class AppDbContext : DbContext
    {
        public DbSet<SpectralLine> SpectralLines { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "optic_ems.db");

            optionsBuilder.UseSqlite($"Data Source={path}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var spectralLineConfiguration = new SpectralLineConfiguration();

            modelBuilder.ApplyConfiguration(spectralLineConfiguration);

            base.OnModelCreating(modelBuilder);
        }
    }
}
