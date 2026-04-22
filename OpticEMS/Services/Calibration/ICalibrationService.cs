using OpticEMS.MVVM.Models.Settings;

namespace OpticEMS.Services.Calibration
{
    public interface ICalibrationService
    {
        double[] CalculateCoefficients(IEnumerable<CalibrationPoint> calibrationPoints);

        void CorrectWavelengthIndices(uint[] intensities, ref int nominalPixel);
    }
}
