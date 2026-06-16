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
            var spectralLine = await _context.SpectralLines
                .AsNoTracking()
                .FirstOrDefaultAsync(line => line.Id == id);

            return spectralLine;
        }

        public async Task<List<SpectralLine>> GetLinesAsync()
        {
            var spectralLines = await _context.SpectralLines
                .AsNoTracking()
                .ToListAsync();

            return spectralLines;
        }

        public async Task<List<SpectralLine>> GetLinesByElementAsync(string element)
        {
            var spectralLines = await _context.SpectralLines
                .Where(line => line.Element == element)
                .OrderBy(line => line.Wavelength)
                .ToListAsync();

            return spectralLines;
        }

        public async Task<List<SpectralLine>> GetLinesByRangeAsync(double minWavelength, double maxWavelength)
        {
            var spectralLines = await _context.SpectralLines
                .Where(line => line.Wavelength >= minWavelength && line.Wavelength <= maxWavelength)
                .ToListAsync();

            return spectralLines;
        }

        public bool ExecuteDeleteLine(int id)
        {
            var result = _context.SpectralLines
                .Where(line => line.Id == id)
                .ExecuteDelete();

            return result > 0;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        public bool ExecuteUpdateLine(SpectralLine line)
        {
            var result = _context.SpectralLines
                .Where(l => l.Id == line.Id)
                .ExecuteUpdate(s => s
                    .SetProperty(l => l.Element, line.Element)
                    .SetProperty(l => l.Wavelength, line.Wavelength)
                    .SetProperty(l => l.ColorHex, line.ColorHex));

            return result > 0;
        }
    }
}
