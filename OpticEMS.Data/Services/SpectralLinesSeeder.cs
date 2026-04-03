using OpticEMS.Contracts.Services.Database;
using OpticEMS.Data.Database.Context;
using System.Reflection;

namespace OpticEMS.Data.Services
{
    public static class SpectralLinesSeeder
    {
        public static async Task SeedFromCsvAsync(AppDbContext context)
        {
            string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string filePath = Path.Combine(exePath, "spectral_lines.csv");

            if (context.SpectralLines.Any())
            {
                return;
            }

            var lines = File.ReadAllLines(filePath)
                .Skip(1)
                .Select(row =>
                {
                    var cleanRow = row.Trim('\"', ' ', '\t');

                    var parts = cleanRow.Split(";");

                    return new SpectralLine
                    {
                        Element = parts[0].Trim(),
                        Wavelength = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                        Ionization = parts[2],
                        ColorHex = parts[3]
                    };
                })
                .ToList();

            if (lines.Any())
            {
                await context.SpectralLines.AddRangeAsync(lines);
            }

            await context.SaveChangesAsync();
        }
    }
}
