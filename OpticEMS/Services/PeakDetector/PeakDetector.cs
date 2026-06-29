using OpticEMS.Contracts.Services.PeakDetector;

namespace OpticEMS.Services.PeakDetector
{
    public class PeakDetector : IPeakDetector
    {
        public IReadOnlyList<PeakPoint> Detect(double[] intensities, double[] wavelengths,
            double threshold = 300, int minDistancePixels = 10)
        {
            if (intensities is null || wavelengths is null || intensities.Length < 3)
            {
                return Array.Empty<PeakPoint>();
            }

            int length = Math.Min(intensities.Length, wavelengths.Length);
            var candidates = new List<PeakPoint>();

            for (int i = 1; i < length - 1; i++)
            {
                double current = intensities[i];

                if (current > threshold
                    && current > intensities[i - 1]
                    && current > intensities[i + 1])
                {
                    candidates.Add(new PeakPoint(wavelengths[i], current, i));
                }
            }

            if (candidates.Count == 0)
            {
                return Array.Empty<PeakPoint>();
            }

            var result = new List<PeakPoint> { candidates[0] };

            for (int i = 1; i < candidates.Count; i++)
            {
                var last = result[^1];
                var current = candidates[i];

                if (current.PixelIndex - last.PixelIndex < minDistancePixels)
                {
                    if (current.Intensity > last.Intensity)
                    {
                        result[^1] = current;
                    }
                }
                else
                {
                    result.Add(current);
                }
            }

            return result;
        }
    }
}
