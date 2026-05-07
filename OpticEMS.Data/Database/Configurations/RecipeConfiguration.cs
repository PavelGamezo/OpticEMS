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
            builder.HasKey(recipe => recipe.DatabaseId);

            builder.Property(recipe => recipe.RecipeId)
                   .IsRequired()
                   .ValueGeneratedNever();

            builder.HasIndex(r => r.RecipeId);

            builder.Property(r => r.ProcessingMode)
                   .HasConversion<int>();

            builder.Property(r => r.DualSubMode)
                   .HasConversion<int>();

            builder.Property(r => r.MultiSubMode)
                   .HasConversion<int>();

            builder.Property(r => r.Wavelengths)
                   .HasConversion(
                       value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                       value => JsonSerializer.Deserialize<List<double>>(value) ?? new());


            builder.Property(r => r.WavelengthNames)
                   .HasConversion(
                       value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                       value => JsonSerializer.Deserialize<List<string>>(value) ?? new());

            builder.Property(r => r.WavelengthColors)
                   .HasConversion(
                       value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                       value => JsonSerializer.Deserialize<List<Color>>(value) ?? new());

            builder.Property(r => r.DetectionWindowHighs)
                   .HasConversion(
                       value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                       value => JsonSerializer.Deserialize<List<int>>(value) ?? new());

            builder.Property(r => r.CombinedNumeratorIndices)
                   .HasConversion(
                       v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                       v => JsonSerializer.Deserialize<List<int>>(v) ?? new());

            builder.Property(r => r.CombinedDenominatorIndices)
                   .HasConversion(
                       v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                       v => JsonSerializer.Deserialize<List<int>>(v) ?? new());
        }
    }
}
