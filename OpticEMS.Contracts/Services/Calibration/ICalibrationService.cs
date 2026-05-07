namespace OpticEMS.Contracts.Services.Calibration
{
    public interface ICalibrationService
    {
        double[] CalculateCoefficients(IEnumerable<CalibrationPoint> calibrationPoints);

        void CorrectWavelengthIndices(double[] intensities, ref int nominalPixel);
    }
}
