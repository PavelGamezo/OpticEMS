using Microsoft.EntityFrameworkCore;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Data.Database.Context;

namespace OpticEMS.Data.Repositories
{
    public class SpectralLineRepository : ISpectralLineRepository
    {
        private readonly AppDbContext _context;

        public SpectralLineRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddLineAsync(SpectralLine line, CancellationToken cancellationToken)
        {
            await _context.AddAsync(line);
        }

        public async Task<SpectralLine?> GetLineByIdAsync(int id, CancellationToken cancellationToken)
        {
            var spectralLine = await _context.SpectralLines.FirstOrDefaultAsync(line => line.Id == id);

            return spectralLine;
        }

        public List<SpectralLine> GetLines()
        {
            var spectralLines = _context.SpectralLines.ToList();

            return spectralLines;
        }

        public List<SpectralLine> GetLinesByElement(string element)
        {
            var spectralLines = _context.SpectralLines
                .Where(line => line.Element == element)
                .OrderBy(line => line.Wavelength)
                .ToList();

            return spectralLines;
        }

        public List<SpectralLine> GetLinesByRange(double minWavelength, double maxWavelength)
        {
            var spectralLines = _context.SpectralLines
                .Where(line => line.Wavelength >= minWavelength && line.Wavelength <= maxWavelength)
                .ToList();

            return spectralLines;
        }

        public void RemoveLine(SpectralLine line)
        {
            _context.Remove(line);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
