namespace OpticEMS.Contracts.Services.Database
{
    public interface ISpectralLineRepository
    {
        Task<SpectralLine?> GetLineByIdAsync(int id, CancellationToken cancellationToken);

        List<SpectralLine> GetLines();

        List<SpectralLine> GetLinesByElement(string element);

        List<SpectralLine> GetLinesByRange(double minWavelength, double maxWavelength);

        Task AddLineAsync(SpectralLine line, CancellationToken cancellationToken);

        void RemoveLine(SpectralLine line);

        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
