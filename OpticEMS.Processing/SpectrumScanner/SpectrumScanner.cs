using OpticEMS.Notifications.Messages;
using Serilog;
using CommunityToolkit.Mvvm.Messaging;

namespace OpticEMS.Processing.SpectrumScanner
{
    public sealed class SpectrumScanner
    {
        private const int SNAPSHOT_INTERVAL_MS = 1000;
        private const int INTENSITY_THRESHOLD = 1000;

        private int _channelId;
        private double _sigmaMultiplier;

        private double[]? _baselineIntensities;
        private double[]? _baselineWavelengths;
        private bool _captureNextAsBaseline;

        private DateTime _lastSnapshotTime = DateTime.MinValue;
        private bool _disposed;

        public bool IsScanning { get; private set; }
        public bool HasBaseline => _baselineIntensities is not null;

        public event Action<SpectrumScannerResult>? ResultReady;

        public void Start(int channelId, double sigmaMultiplier = 3.0)
        {
            if (IsScanning)
            {
                return;
            }

            _channelId = channelId;
            _sigmaMultiplier = sigmaMultiplier;
            _captureNextAsBaseline = true;
            _baselineIntensities = null;
            _baselineWavelengths = null;
            _lastSnapshotTime = DateTime.MinValue;
            IsScanning = true;

            WeakReferenceMessenger.Default.Register<SpectrumUpdatedMessage>(
                this, (_, message) => HandleSpectrum(message));

            Log.Information("[SPECTRUM_SCANNER]: Started for channel {Id}, σ×{N}",
                channelId, sigmaMultiplier);
        }

        public void Stop()
        {
            if (!IsScanning)
            {
                return;
            }

            IsScanning = false;
            WeakReferenceMessenger.Default.Unregister<SpectrumUpdatedMessage>(this);

            Log.Information("[SPECTRUM_SCANNER]: Stopped for channel {Id}", _channelId);
        }

        private void HandleSpectrum(SpectrumUpdatedMessage message)
        {
            if (message.ChannelId != _channelId)
            {
                return;
            }

            if (message.Intensities is null || message.Intensities.Length == 0)
            {
                return;
            }

            if (_captureNextAsBaseline)
            {
                _baselineWavelengths = (double[])message.Wavelengths.Clone();
                _baselineIntensities = (double[])message.Intensities.Clone();
                _captureNextAsBaseline = false;

                Log.Information("[SPECTRUM_SCANNER]: Baseline captured ({Points} points)",
                    message.Intensities.Length);

                return;
            }

            if (_baselineIntensities is null)
            {
                return;
            }

            double elapsed = (DateTime.Now - _lastSnapshotTime).TotalMilliseconds;
            if (elapsed < SNAPSHOT_INTERVAL_MS)
            {
                return;
            }

            _lastSnapshotTime = DateTime.Now;

            var result = Analyze(message.Wavelengths, message.Intensities);
            ResultReady?.Invoke(result);
        }

        private SpectrumScannerResult Analyze(double[] wavelengths, double[] intensities)
        {
            int length = Math.Min(_baselineIntensities!.Length, intensities.Length);

            var rawDiff = new double[length];
            for (int i = 0; i < length; i++)
            {
                rawDiff[i] = intensities[i] - _baselineIntensities[i];
            }

            var filteredDiff = new double[length];
            int peakCount = 0;

            for (int i = 0; i < length; i++)
            {
                if (rawDiff[i] > INTENSITY_THRESHOLD)
                {
                    filteredDiff[i] = rawDiff[i];
                    peakCount++;
                }
            }

            return new SpectrumScannerResult(wavelengths, filteredDiff, 1000);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
        }
    }
}
