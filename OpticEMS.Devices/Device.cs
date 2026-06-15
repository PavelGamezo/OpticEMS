using OpticEMS.Contracts.Services.Settings;

namespace OpticEMS.Devices
{
    public abstract class Device
    {
        public abstract DeviceInfo DeviceInfo { get; set; }

        public abstract void Initialize(int id);

        public abstract bool Scan(int id, double[] collection, CancellationToken cancellationToken);

        public abstract void SetParameters(int id, float exposureMs, int scansNum, float equalizer, int mode = 0);

        public abstract void StopMeasurement();

    }
}
