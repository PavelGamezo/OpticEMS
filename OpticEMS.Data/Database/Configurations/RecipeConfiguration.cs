using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticEMS.Contracts.Services.Recipe;
using System.Text.Json;
using System.Windows.Media;

namespace OpticEMS.Data.Database.Configurations
{
    public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
    {
        public void Configure(EntityTypeBuilder<Recipe> builder)
        {
            builder.HasKey(recipe => recipe.Id);
            
            builder.Property(recipe => recipe.Id)
                .ValueGeneratedNever();

            builder.Property(r => r.Wavelengths)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<List<double>>(value) ?? new());

            builder.Property(r => r.WavelengthColors)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<List<Color>>(value) ?? new());

            builder.Property(r => r.DetectionWindowHighs)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<List<int>>(value) ?? new());
        }
    }
}
