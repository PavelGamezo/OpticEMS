using Microsoft.EntityFrameworkCore;
using OpticEMS.Contracts.Services.Recipe;
using OpticEMS.Data.Database.Configurations;

namespace OpticEMS.Data.Database.Context
{
    public class RecipeDbContext : DbContext
    {
        public DbSet<Recipe> Recipes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "optic_ems_recipes.db");

            optionsBuilder.UseSqlite($"Data Source={path}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var recipeConfiguration = new RecipeConfiguration();

            modelBuilder.ApplyConfiguration(recipeConfiguration);

            base.OnModelCreating(modelBuilder);
        }
    }
}
