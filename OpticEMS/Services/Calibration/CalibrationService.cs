using MathNet.Numerics;
using OpticEMS.Contracts.Services.Calibration;
using Serilog;

namespace OpticEMS.Services.Calibration
{
    public class CalibrationService : ICalibrationService
    {
        private const int CORRECTION_PIXELS_WINDOW = 2;
        private const double MIN_INTENSITY_THRESHOLD = 500;
        private const double MIN_PEAK_PROMINENCE = 1.2;

        public double[] CalculateCoefficients(IEnumerable<CalibrationPoint> calibrationPoints)
        {
            try
            {
                var pixels = calibrationPoints.Select(point => (double)point.Pixel).ToArray();
                var wavelengths = calibrationPoints.Select(point => point.Wavelength).ToArray();

                var coefficients = Polynomial.Fit(
                    pixels,
                    wavelengths,
                    3,
                    MathNet.Numerics.LinearRegression.DirectRegressionMethod.QR).Coefficients;

                return coefficients;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[CALIBRATION_COEFFICIENTS]: Failed to calculate calibration coefficients");
                throw;
            }
        }

        public void CorrectWavelengthIndices(double[] intensities, ref int nominalPixel)
        {
            try
            {
                if (intensities == null || nominalPixel < 0 || nominalPixel >= intensities.Length)
                {
                    return;
                }

                int startIdx = Math.Max(0, nominalPixel - CORRECTION_PIXELS_WINDOW);
                int endIdx = Math.Min(intensities.Length - 1, nominalPixel + CORRECTION_PIXELS_WINDOW);

                int bestPixel = nominalPixel;
                double maxIntensity = intensities[nominalPixel];
                bool foundValidPeak = false;

                for (int offset = startIdx; offset <= endIdx; offset++)
                {
                    if (intensities[offset] > maxIntensity)
                    {
                        double leftNeighbor = (offset > 0) ? intensities[offset - 1] : 0;
                        double rightNeighbor = (offset < intensities.Length - 1) ? intensities[offset + 1] : 0;
                        if (intensities[offset] > leftNeighbor && intensities[offset] > rightNeighbor)
                        {
                            maxIntensity = intensities[offset];
                            bestPixel = offset;
                            foundValidPeak = true;
                        }
                    }
                }

                double background = (intensities[startIdx] + intensities[endIdx]) / 2.0;

                if (maxIntensity < MIN_INTENSITY_THRESHOLD || maxIntensity < background * MIN_PEAK_PROMINENCE)
                {
                    return;
                }

                nominalPixel = bestPixel;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[PEAK_SEARCH]: Failed to calculate correction peak");
                throw;
            }
        }
    }
}
