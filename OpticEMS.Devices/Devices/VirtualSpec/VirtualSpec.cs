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

        public override void SetParameters(int id, float exposureMs, int scansNum, int mode)
        {
            _exposureMs = exposureMs;
        }

        public override bool Scan(int id, double[] collection, CancellationToken cancellationToken)
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

        private double[] GenerateSpectrum()
        {
            double[] data = new double[PIXELS];

            double elapsed = _isRunning ? (DateTime.Now - _startTime).TotalSeconds : 0;

            double[] currentCoef = { 344.56, 0.379, -5.33E-06, -1.02E-08 };

            double magneticPeriod = 2.0;
            double dropStart = 12.0;
            double dropEnd = 25.0;
            double startIntensity = 5000.0;
            double noiseFloor = 150.0;

            double magneticRipple = 1.0 + 0.03 * Math.Sin(2 * Math.PI * elapsed / magneticPeriod);

            for (int i = 0; i < PIXELS; i++)
            {
                double value = (100 + (_rnd.NextDouble() * 50)) * magneticRipple;

                foreach (var line in _lines)
                {
                    double px = MapWavelengthToPixel(line.wavelength, currentCoef);
                    double modIntensity;

                    if (Math.Abs(line.wavelength - 365.015) < 1.0)
                    {
                        if (!_isRunning)
                        {
                            modIntensity = startIntensity;
                        }
                        else if (elapsed < dropStart)
                        {
                            modIntensity = startIntensity - (elapsed * 2.0);
                        }
                        else if (elapsed < dropEnd)
                        {
                            double progress = (elapsed - dropStart) / (dropEnd - dropStart);
                            double factor = Math.Pow(0.01, progress);
                            modIntensity = (startIntensity * factor) + noiseFloor;
                        }
                        else
                        {
                            modIntensity = noiseFloor + (_rnd.NextDouble() * 20);
                        }
                    }
                    else
                    {
                        modIntensity = line.intensity * 1500;
                    }

                    double currentSignal = modIntensity * magneticRipple;

                    double dx = i - px;
                    double signal = currentSignal * Math.Exp(-(dx * dx) / (2 * 1.2 * 1.2));

                    double shotNoise = (_rnd.NextDouble() - 0.5) * Math.Sqrt(signal + 1) * 2.0;

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
