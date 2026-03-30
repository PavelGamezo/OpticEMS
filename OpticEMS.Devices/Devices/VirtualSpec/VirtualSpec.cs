using OpticEMS.Contracts.Services.Settings;

namespace OpticEMS.Devices.Devices.VirtualSpec
{
    public class VirtualSpec : Device
    {
        private readonly object @lock = new object();
        private const int PIXELS = 2048;

        private double _phase = 1;
        private DateTime _startTime;
        private bool _isRunning;
        private float _exposureMs = 5f;
        private readonly Random _rnd = new();

        private readonly (double wavelength, double intensity)[] _lines =
        {
            (365.015, 1.0), (404.656, 0.8), (435.833, 1.2),
            (546.074, 1.5), (576.960, 0.6), (579.066, 0.6),
            (696.543, 0.7), (706.722, 0.35), (738.398, 0.8),
            (750.387, 0.9), (763.511, 1.3), (772.376, 0.7),
            (794.818, 0.5), (800.616, 0.6), (811.531, 0.9)
        };

        public override DeviceInfo DeviceInfo { get; set; }

        public VirtualSpec(int id)
        {
            Initialize(id);
        }

        public override void Initialize(int id)
        {
            lock (@lock)
            {
                DeviceInfo = new DeviceInfo(
                    "VIRTUAL-SPEC-001",
                    PIXELS, 
                    id,
                    -1,
                    DeviceType.VirtualSpec,
                    10,
                    0,
                    -1.029096988621741E-08,
                    -5.332134891649228E-06,
                    0.3790476108105803,
                    344.5635979788548,
                    0);

                _startTime = DateTime.Now;
            }
        }

        public override void SetParameters(int id, float exposureMs, int scansNum)
        {
            _exposureMs = exposureMs;
        }

        public override bool Scan(int id, uint[] collection, CancellationToken cancellationToken)
        {
            Thread.Sleep((int)_exposureMs);

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            lock (@lock)
            {
                var spectrum = GenerateSpectrum();
                Array.Copy(spectrum, 0, collection, 0, Math.Min(PIXELS, collection.Length));
                return true;
            }
        }

        public void StartProcess()
        {
            _phase = 1;
            _isRunning = true;
            _startTime = DateTime.Now;
        }

        public void PauseProcess() => _isRunning = !_isRunning;

        public void StopProcess()
        {
            _phase = 1;
            _isRunning = false;
        }

        private uint[] GenerateSpectrum()
        {
            uint[] data = new uint[PIXELS];
            var elapsed = (DateTime.Now - _startTime).TotalSeconds;

            if (_isRunning && elapsed > 10.0) _phase += 0.0005;
            double[] currentCoef = { 344.56, 0.379, -5.33E-06, -1.02E-08 };

            for (int i = 0; i < PIXELS; i++)
            {
                double value = 600 + (_rnd.NextDouble() * 150 - 10);

                foreach (var line in _lines)
                {
                    double px = MapWavelengthToPixel(line.wavelength, currentCoef);
                    double modIntensity = line.intensity;

                    if (Math.Abs(line.wavelength - 365) < 1.0)
                    {
                        modIntensity *= (1.0 + 0.5 * Math.Sin(_phase * 1.5));
                    }
                    else if (Math.Abs(line.wavelength - 404) < 1.0)
                    {
                        modIntensity *= (1.0 + 0.5 * Math.Sin(_phase * 1.5 + Math.PI));
                    }

                    double dx = i - px;
                    double sigma = 1.2;
                    double signal = 3000 * modIntensity * Math.Exp(-(dx * dx) / (2 * sigma * sigma));

                    double shotNoise = (_rnd.NextDouble() - 0.5) * Math.Sqrt(signal) * 2.0;

                    value += signal + shotNoise;
                }

                data[i] = (uint)Math.Clamp(value, 0, 65535);
            }
            return data;
        }

        private double MapWavelengthToPixel(double wl, double[] c)
        {
            return (wl - c[0]) / c[1];
        }

        public override void StopMeasurement() => _isRunning = false;
    }
}
