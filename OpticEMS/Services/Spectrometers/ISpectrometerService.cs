namespace OpticEMS.Services.Spectrometers
{
    public interface ISpectrometerService
    {
        void RequestSingleScan(int cameraId);

        string? GetSerialNumber(int cameraId);

        int GetConnectedSpectrometersCount();

        bool IsSpectrometerInitialized();

        uint[]? GetSpectrometerData(int cameraId, float exposureMs = 5f);
    }
}
