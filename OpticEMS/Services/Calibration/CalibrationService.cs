using MathNet.Numerics;
using OpticEMS.MVVM.Models;

namespace OpticEMS.Services.Calibration
{
    public class CalibrationService : ICalibrationService
    {
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
    }
}
