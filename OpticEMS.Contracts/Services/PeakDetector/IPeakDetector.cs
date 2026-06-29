namespace OpticEMS.Contracts.Services.PeakDetector
{
    public interface IPeakDetector
    {
        IReadOnlyList<PeakPoint> Detect(
            double[] intensities,
            double[] wavelengths,
            double threshold = 300,
            int minDistancePixels = 10);
    }
}
