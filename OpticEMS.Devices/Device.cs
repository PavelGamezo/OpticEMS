using OpticEMS.Contracts.Services.Settings;

namespace OpticEMS.Devices
{
    public abstract class Device
    {
        public abstract DeviceInfo DeviceInfo { get; set; }

        public abstract void Initialize(int id);

        public abstract bool Scan(int id, uint[] collection, CancellationToken cancellationToken);

        public abstract void SetParameters(int id, float exposureMs, int scansNum);

        public abstract void StopMeasurement();

    }
}
