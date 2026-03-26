namespace OpticEMS.Services.Calibration
{
    public interface IWavelengthMapper
    {
        double[]? Wavelengths { get; }

        double[] ConvertPixelsToWavelengths(uint[] spectrum, double[]? coefficients);

        int FindNearestIndex(double[] array, double target);

        double FindWavelengthByPixel(uint pixel, double[]? coefficients);
    }
}
