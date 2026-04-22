using MathNet.Numerics;
using OpticEMS.MVVM.Models.Settings;

namespace OpticEMS.Services.Calibration
{
    public class CalibrationService : ICalibrationService
    {
        private const int CORRECTION_PIXELS_WINDOW = 2;

        public double[] CalculateCoefficients(IEnumerable<CalibrationPoint> calibrationPoints)
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

        public void CorrectWavelengthIndices(uint[] intensities, ref int nominalPixel)
        {
            if (intensities == null || nominalPixel < 0 || nominalPixel >= intensities.Length)
            {
                return;
            }

            int bestPixel = nominalPixel;
            uint maxIntensity = intensities[nominalPixel];

            for (int offset = -CORRECTION_PIXELS_WINDOW; offset <= CORRECTION_PIXELS_WINDOW; offset++)
            {
                int idx = nominalPixel + offset;
                if (idx < 0 || idx >= intensities.Length) 
                {
                    continue;
                }

                bool isLocalMax = intensities[idx] > maxIntensity &&
                                  (idx == 0 || intensities[idx] > intensities[idx - 1]) &&
                                  (idx == intensities.Length - 1 || intensities[idx] > intensities[idx + 1]);

                if (isLocalMax)
                {
                    maxIntensity = intensities[idx];
                    bestPixel = idx;
                }
            }

            nominalPixel = bestPixel;
        }
    }
}
