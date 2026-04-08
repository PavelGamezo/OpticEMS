namespace OpticEMS.Contracts.Services.Database
{
    public interface ISpectralLineRepository
    {
        Task<SpectralLine?> GetLineByIdAsync(int id, CancellationToken cancellationToken);

        Task<List<SpectralLine>> GetLinesAsync();

        Task<List<SpectralLine>> GetLinesByElementAsync(string element);

        Task<List<SpectralLine>> GetLinesByRangeAsync(double minWavelength, double maxWavelength);

        Task AddLineAsync(SpectralLine line, CancellationToken cancellationToken);

        void RemoveLine(SpectralLine line);

        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
