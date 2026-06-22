namespace OpticEMS.Contracts.Services.SpectrumScan
{
    public record SpectrumScanSnapshot(
        double[] Wavelengths,
        double[] BaselineIntensities,
        double[] CurrentIntensities,
        double[] DiffIntensities);
}
