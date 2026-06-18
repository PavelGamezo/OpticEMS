using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticEMS.Contracts.Services.Settings;
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

        private float _exposureMs = 1;

        private int _scansNum = 1;

        private float _equalizer = 1;

        public float ExposureMs
        {
            get => _exposureMs;
            set
            {
                if (SetProperty(ref _exposureMs, value))
                {
                    if (value > 0)
                    {
                        SaveSpectrometerParams();
                    }
                }
            }
        }

        public int ScansNum
        {
            get => _scansNum;
            set
            {
                if (SetProperty(ref _scansNum, value))
                {
                    if (value > 0)
                    {
                        SaveSpectrometerParams();
                    }
                }
            }
        }

        public float Equalizer
        {
            get => _equalizer;
            set
            {
                if (SetProperty(ref _equalizer, value))
                {
                    if (value > 0)
                    {
                        SaveSpectrometerParams();
                    }
                }
            }
        }

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
                    var exposureMs = device.ExposureTime;
                    var scansNum = device.ScansNum;
                    var equalizer = device.Equalizer;

                    ExposureMs = exposureMs;
                    ScansNum = scansNum;
                    Equalizer = equalizer;

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
    }
}
