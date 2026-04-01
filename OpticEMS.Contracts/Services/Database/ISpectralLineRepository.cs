namespace OpticEMS.Contracts.Services.Database
{
    public interface ISpectralLineRepository
    {
        Task<SpectralLine?> GetLineByIdAsync(int id, CancellationToken cancellationToken);

        IEnumerable<SpectralLine> GetLinesAsync();

        IEnumerable<SpectralLine> GetLinesByElementAsync(string element);

        IEnumerable<SpectralLine> GetLinesByRangeAsync(double minWavelength, double maxWavelength);

        Task AddLineAsync(SpectralLine line, CancellationToken cancellationToken);

        void RemoveLine(SpectralLine line);

        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
