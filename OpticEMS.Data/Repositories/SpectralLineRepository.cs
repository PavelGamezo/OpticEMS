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

        public IEnumerable<SpectralLine> GetLinesAsync()
        {
            var spectralLines = _context.SpectralLines.AsEnumerable();

            return spectralLines;
        }

        public IEnumerable<SpectralLine> GetLinesByElementAsync(string element)
        {
            var spectralLines = _context.SpectralLines
                .Where(line => line.Element == element)
                .OrderBy(line => line.Wavelength)
                .AsEnumerable();

            return spectralLines;
        }

        public IEnumerable<SpectralLine> GetLinesByRangeAsync(double minWavelength, double maxWavelength)
        {
            var spectralLines = _context.SpectralLines
                .Where(line => line.Wavelength >= minWavelength && line.Wavelength <= maxWavelength)
                .AsEnumerable();

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
