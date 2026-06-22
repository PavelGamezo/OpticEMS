using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services.SpectrumScan;
using OpticEMS.Notifications.Messages;
using Serilog;

namespace OpticEMS.MVVM.Models.SpectrumScan
{
    public class SpectrumScanModel
    {
        private bool _captureNextAsBaseline;
        private double[]? _baselineIntensities;
        private double[]? _baselineWavelengths;
        private bool _disposed;

        public int ChannelId { get; private set; }
        public bool IsScanning { get; private set; }
        public bool HasBaseline => _baselineIntensities is not null;
        public SpectrumScanSnapshot? LastSnapshot { get; private set; }

        public event Action<SpectrumScanSnapshot>? SnapshotReady;

        public SpectrumScanModel(int initialChannelId)
        {
            ChannelId = initialChannelId;
            RegisterMessages();
        }

        public void StartScan()
        {
            if (IsScanning)
            {
                return;
            }

            Log.Information("[SPECTRUM_SCAN]: Scan started for channel {ChannelId}", ChannelId);

            IsScanning = true;
            _captureNextAsBaseline = true;
        }

        public void StopScan()
        {
            if (!IsScanning)
            {
                return;
            }

            Log.Information("[SPECTRUM_SCAN]: Scan stopped for channel {ChannelId}", ChannelId);
            IsScanning = false;
        }

        public void ResetBaseline()
        {
            if (!IsScanning)
            {
                Log.Warning("[SPECTRUM_SCAN]: ResetBaseline ignored - scan is not running.");
                return;
            }

            Log.Information("[SPECTRUM_SCAN]: Baseline reset requested for channel {ChannelId}", ChannelId);
            _captureNextAsBaseline = true;
        }

        private void ClearBaseline()
        {
            _baselineIntensities = null;
            _baselineWavelengths = null;
            LastSnapshot = null;
        }

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<SpectrumUpdatedMessage>(this, (_, message) =>
            {
                if (message.ChannelId != ChannelId || !IsScanning)
                {
                    return;
                }

                if (message.Intensities is null || message.Intensities.Length == 0)
                {
                    return;
                }

                HandleIncomingSpectrum(message.Wavelengths, message.Intensities);
            });
        }

        private void HandleIncomingSpectrum(double[] wavelengths, double[] intensities)
        {
            if (_captureNextAsBaseline)
            {
                CaptureBaseline(wavelengths, intensities);
                _captureNextAsBaseline = false;
                return;
            }

            if (_baselineIntensities is null)
            {
                return;
            }

            var snapshot = BuildSnapshot(wavelengths, intensities);
            LastSnapshot = snapshot;
            SnapshotReady?.Invoke(snapshot);
        }

        private void CaptureBaseline(double[] wavelengths, double[] intensities)
        {
            _baselineWavelengths = (double[])wavelengths.Clone();
            _baselineIntensities = (double[])intensities.Clone();

            Log.Information("[SPECTRUM_SCAN]: Baseline captured for channel {ChannelId} ({Points} points)",
                ChannelId, intensities.Length);
        }

        private SpectrumScanSnapshot BuildSnapshot(double[] wavelengths, double[] currentIntensities)
        {
            int length = Math.Min(_baselineIntensities!.Length, currentIntensities.Length);
            var diff = new double[length];

            for (int i = 0; i < length; i++)
            {
                diff[i] = currentIntensities[i] - _baselineIntensities[i];
            }

            return new SpectrumScanSnapshot(
                Wavelengths: wavelengths,
                BaselineIntensities: _baselineIntensities,
                CurrentIntensities: currentIntensities,
                DiffIntensities: diff);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
    }
}
