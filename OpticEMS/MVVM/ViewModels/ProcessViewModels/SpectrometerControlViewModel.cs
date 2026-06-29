using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Notifications.Messages;
using OpticEMS.Orchestrator;
using Serilog;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class SpectrometerControlViewModel : ObservableObject
    {
        private int _channelId;

        private readonly EtchingOrchestrator _orchestrator;
        private readonly ISettingsProvider _settingsProvider;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StopSpectrometerScanCommand))]
        private bool _isScanning = true;
        [ObservableProperty] private float _exposureMs = 1;
        [ObservableProperty] private int _scansNum = 1;
        [ObservableProperty] private float _equalizer = 1;
        [ObservableProperty] private bool _isPeakModeEnabled;

        private bool CanStopScan => IsScanning;

        public SpectrometerControlViewModel(
            int channeldId,
            EtchingOrchestrator orchestrator,
            ISettingsProvider settingsProvider)
        {
            _orchestrator = orchestrator;
            _settingsProvider = settingsProvider;

            LoadSpectrometerParams();
        }

        [RelayCommand]
        public async Task StartSpectrometerScan()
        {
            Log.Information("[SPECTROMETER_CONTROL]: Start spectrometer scan request.");

            try
            {
                await _orchestrator.StartSpectrometerScan(ExposureMs, ScansNum, Equalizer);

                IsScanning = true;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[SPECTROMETER_CONTROL]: Start spectrometer scan request error.");
            }
        }

        [RelayCommand(CanExecute = nameof(CanStopScan))]
        public async Task StopSpectrometerScan()
        {
            Log.Information("[SPECTROMETER_CONTROL]: Stop spectrometer scan request.");

            try
            {
                await _orchestrator.StopSpectrometerScan();

                IsScanning = false;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[SPECTROMETER_CONTROL]: Stop spectrometer scan request error.");
            }
        }

        private void LoadSpectrometerParams()
        {
            Log.Information("[SPECTROMETER_CONTROL]: Loading spectrometer params request from device settings.");
            
            try
            {
                var device = _settingsProvider.GetByChannelId(_channelId);

                if (device != null)
                {
                    _exposureMs = device.ExposureTime;
                    _scansNum = device.ScansNum;
                    _equalizer = device.Equalizer;
                    _isPeakModeEnabled = device.PeakModeEnabled;

                    OnPropertyChanged(nameof(ExposureMs));
                    OnPropertyChanged(nameof(ScansNum));
                    OnPropertyChanged(nameof(Equalizer));
                    OnPropertyChanged(nameof(IsPeakModeEnabled));

                    Log.Information("[SPECTROMETER_CONTROL]: Spectrometer params loaded from device settings.");
                }
                else
                {
                    Log.Warning("[SPECTROMETER_CONTROL]: Device is not initializer or can't be deserialized from application settings.");
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[SPECTROMETER_CONTROL]: Error during spectrometer params loading.");
            }
        }

        private void SaveSpectrometerParams()
        {
            Log.Information("[SPECTROMETER_CONTROL]: Saving spectrometer params request to application settings.");

            try
            {
                var device = _settingsProvider.GetByChannelId(_channelId);

                if (device != null)
                {
                    device.ExposureTime = ExposureMs;
                    device.ScansNum = ScansNum;
                    device.Equalizer = Equalizer;
                    device.PeakModeEnabled = IsPeakModeEnabled;

                    _settingsProvider.Upsert(device);
                    _settingsProvider.Save();
                }
                else
                {
                    Log.Warning("[SPECTROMETER_CONTROL]: Device is not initializer or can't be deserialized from application settings.");
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[SPECTROMETER_CONTROL]: Error during spectrometer params saving.");
            }
        }

        partial void OnIsPeakModeEnabledChanged(bool value)
        {
            Log.Information("[SPECTROMETER_CONTROL]: Peak mode {State}.", value ? "enabled" : "disabled");
            WeakReferenceMessenger.Default.Send(new PeakModeChangedMessage(_channelId, value));
            SaveSpectrometerParams();
        }

        partial void OnExposureMsChanged(float value)
        {
            if (value > 0) SaveSpectrometerParams();
        }

        partial void OnScansNumChanged(int value)
        {
            if (value > 0) SaveSpectrometerParams();
        }

        partial void OnEqualizerChanged(float value)
        {
            if (value > 0) SaveSpectrometerParams();
        }
    }
}
