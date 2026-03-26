using OpticEMS.MVVM.Models;

namespace OpticEMS.Services.Calibration
{
    public interface ICalibrationService
    {
        double[] CalculateCoefficients(IEnumerable<CalibrationPoint> calibrationPoints);
    }
}
