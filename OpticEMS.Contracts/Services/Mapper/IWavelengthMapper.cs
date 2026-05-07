namespace OpticEMS.Contracts.Services.Mapper
{
    public interface IWavelengthMapper
    {
        double[]? Wavelengths { get; }

        double[] ConvertPixelsToWavelengths(double[] spectrum, double[]? coefficients);

        int FindNearestIndex(double[] array, double target);

        double FindWavelengthByPixel(double pixel, double[]? coefficients);
    }
}
