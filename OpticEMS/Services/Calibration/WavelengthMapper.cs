namespace OpticEMS.Services.Calibration
{
    public class WavelengthMapper : IWavelengthMapper
    {
        public double[]? Wavelengths { get; private set; }

        public int FindNearestIndex(double[] array, double target)
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

        public double[] ConvertPixelsToWavelengths(uint[] spectrum, double[]? coefficients)
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
                return Wavelengths;
            }
        }

        public double FindWavelengthByPixel(uint pixel, double[]? coefficients)
        {
            var wavelength = coefficients[3]
                    + coefficients[2] * pixel
                    + coefficients[1] * Math.Pow(pixel, 2)
                    + coefficients[0] * Math.Pow(pixel, 3);

            return wavelength;
        }
    }
}
