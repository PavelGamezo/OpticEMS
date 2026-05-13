using OpticEMS.Contracts.Services.Mapper;
using Serilog;

namespace OpticEMS.Services.Calibration
{
    public class WavelengthMapper : IWavelengthMapper
    {
        public double[]? Wavelengths { get; private set; }

        public int FindNearestIndex(double[] array, double target)
        {
            try
            {
                var bestIndex = 0;
                var minDiff = double.MaxValue;

                for (int i = 0; i < array.Length; i++)
                {
                    var diff = Math.Abs(array[i] - target);

                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        bestIndex = i;
                    }
                }

                return bestIndex;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[CORRECTION_INDEX]: Error searching target {Target}", target);
                throw;
            }
        }

        public double[] ConvertPixelsToWavelengths(double[] spectrum, double[]? coefficients)
        {
            if (Wavelengths is null)
            {
                double[] wavelengths = new double[spectrum.Length];

                for (int pixel = 0; pixel < spectrum.Length; pixel++)
                {
                    wavelengths[pixel] = coefficients[3]
                        + coefficients[2] * pixel
                        + coefficients[1] * Math.Pow(pixel, 2)
                        + coefficients[0] * Math.Pow(pixel, 3);
                }

                Wavelengths = wavelengths;

                return wavelengths;
            }
            else
            {
                Log.Warning("[WAVELENGTH_CONVERTION]: Wavelengths are invalid");
                return Wavelengths;
            }
        }

        public double FindWavelengthByPixel(double pixel, double[]? coefficients)
        {
            try
            {
                var wavelength = coefficients[3]
                    + coefficients[2] * pixel
                    + coefficients[1] * Math.Pow(pixel, 2)
                    + coefficients[0] * Math.Pow(pixel, 3);

                Log.Information("[WAVELENGTH_CALCULATION]: Pixel {Pixel} -> {Wavelength} nm", pixel, wavelength);
                
                return wavelength;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[WAVELENGTH_CALCULATION]: Error to find wavelength by pixel");
                throw;
            }
        }
    }
}
