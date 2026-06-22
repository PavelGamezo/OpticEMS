namespace OpticEMS.Processing.SpectrumScanner
{
    public sealed record SpectrumScannerResult(
        double[] Wavelengths,
        double[] Intensities,
        double Threshold);
}
