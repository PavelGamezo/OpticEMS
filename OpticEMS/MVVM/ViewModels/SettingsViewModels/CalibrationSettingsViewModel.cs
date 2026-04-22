using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.MVVM.Models.Settings;
using OpticEMS.Notifications.Messages;
using OpticEMS.Services.Calibration;
using OpticEMS.Services.Dialogs;
using OpticEMS.Services.Settings;
using OpticEMS.Services.Spectrometers;
using System.Collections.ObjectModel;

namespace OpticEMS.MVVM.ViewModels.SettingsViewModels
{
    public partial class CalibrationSettingsViewModel : ObservableObject
    {
        private readonly ISpectrometerService _spectrometerService;
        private readonly IWavelengthMapper _wavelengthMapper;
        private readonly IDialogService _dialogService;
        private readonly ICalibrationService _calibrationService;

        private uint[]? _currentSpectrumData;

        public CalibrationSettingsChartViewModel CalibrationSettingsChartViewModel { get; }

        [ObservableProperty]
        private ObservableCollection<int> _availableChannels = new();

        [ObservableProperty]
        private int _selectedChannel;

        [ObservableProperty]
        private ObservableCollection<CalibrationPoint> _calibrationPoints = new();

        [ObservableProperty]
        private ObservableCollection<double> _calibrationCoefficients = new();

        [ObservableProperty]
        private double _calibrationWavelength = 0;

        [ObservableProperty]
        private double _calibrationPixel = 0;

        public string CalibrationStatus => CalibrationCoefficients.Count >= 4
            ? "Calibrated chamber"
            : $"Ready for calibration";

        public string StatusColor => CalibrationCoefficients.Any(coefficient => coefficient.Equals(0))
            ? "#E74C3C"
            : "#27AE60";

        public CalibrationSettingsViewModel(ICalibrationService calibrationService,
            IWavelengthMapper wavelengthMapper,
            IDialogService dialogService,
            ISpectrometerService spectrometerService)
        {
            _calibrationService = calibrationService;
            _wavelengthMapper = wavelengthMapper;
            _dialogService = dialogService;
            _spectrometerService = spectrometerService;

            CalibrationSettingsChartViewModel = new CalibrationSettingsChartViewModel();
            GetSetupSettings();
            RegisterMessages();

            CalibrationPoints.CollectionChanged += (s, e) => CalculateCalibrationCommand.NotifyCanExecuteChanged();

            RefreshChannels();
        }

        [RelayCommand(CanExecute = nameof(CanInterpolate))]
        private void Interpolation()
         {
            if (_currentSpectrumData == null || CalibrationCoefficients.Count < 4)
            {
                _dialogService.ShowInformation("No spectrum data or insufficient calibration coefficients. " +
                    "Please capture a spectrum and calculate calibration first.");
            }

            var deviceInfo = AppSettings.Default.Devices.FirstOrDefault(device => device.ChannelId == SelectedChannel);

            var coefficients = new double[] 
            {
                deviceInfo.CoefA,
                deviceInfo.CoefB, 
                deviceInfo.CoefC, 
                deviceInfo.CoefD 
            };

            var wavelengths = _wavelengthMapper.ConvertPixelsToWavelengths(_currentSpectrumData, coefficients);

            CalibrationSettingsChartViewModel.UpdateInterpolationPlot(_currentSpectrumData, wavelengths);
        }

        [RelayCommand(CanExecute = nameof(CanCalculate))]
        private void CalculateCalibration()
        {
            var coefficients = _calibrationService.CalculateCoefficients(CalibrationPoints);

            CalibrationCoefficients.Clear();

            foreach (var coefficient in coefficients)
            {
                CalibrationCoefficients.Add(coefficient);
            }

            var devices = AppSettings.Default.Devices;

            var deviceInfo = devices.FirstOrDefault(device => device.ChannelId == SelectedChannel);

            if (deviceInfo != null)
            {
                deviceInfo.CoefA = coefficients[3];
                deviceInfo.CoefB = coefficients[2];
                deviceInfo.CoefC = coefficients[1];
                deviceInfo.CoefD = coefficients[0];

                AppSettings.Default.Devices = devices;

                AppSettings.Default.Save();

                CalibrationCoefficients.Clear();

                foreach (var coefficient in coefficients)
                {
                    CalibrationCoefficients.Add(coefficient);
                }
            }

            Interpolation();
        }

        [RelayCommand]
        private void AddPoint()
        {
            if (CalibrationWavelength > 0 && CalibrationPixel >= 0)
            {
                var point = new CalibrationPoint(CalibrationPixel, CalibrationWavelength);
                CalibrationPoints.Add(point);

                CalibrationPixel = 0;
                CalibrationWavelength = 0;
            }
        }

        [RelayCommand]
        private void RemovePoint(CalibrationPoint point)
        {
            if (point != null)
            {
                CalibrationPoints.Remove(point);
            }
        }

        [RelayCommand]
        private void ClearPoints() => CalibrationPoints.Clear();

        [RelayCommand]
        private async Task CaptureSpectrumAsync()
        {
            CalibrationSettingsChartViewModel.ResetPlotAxes();

            _spectrometerService.RequestSingleScan(SelectedChannel);
        }

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<CalibrationChartUpdatedMessage>(this, (recipient, message) =>
            {
                if (message.Id != this.SelectedChannel)
                {
                    return;
                }

                HandleIncomingSpectrum(message.Intensities);
            });
        }

        private void HandleIncomingSpectrum(uint[] intensities)
        {
            _currentSpectrumData = intensities;
            CalibrationSettingsChartViewModel.UpdateCalibrationPlot(_currentSpectrumData);
        }

        private void RefreshChannels()
        {
            AvailableChannels.Clear();
            foreach (var dev in AppSettings.Default.Devices)
            {
                AvailableChannels.Add(dev.ChannelId);
            }

            if (AvailableChannels.Any())
                SelectedChannel = AvailableChannels.First();
        }

        private bool CanCalculate() => CalibrationPoints.Count >= 4;

        private bool CanInterpolate() => CalibrationCoefficients.All(coefficient => coefficient != 0);

        partial void OnSelectedChannelChanged(int value)
        {
            CalibrationPoints.Clear();
            CalibrationCoefficients.Clear();

            _currentSpectrumData = null;

            CalibrationSettingsChartViewModel.ResetPlotSeries();

            LoadCoefficientsForSelectedChannel();
        }

        private void LoadCoefficientsForSelectedChannel()
        {
            var deviceConfig = AppSettings.Default.Devices.FirstOrDefault(device => device.ChannelId == SelectedChannel);

            CalibrationCoefficients.Clear();

            if (deviceConfig != null)
            {
                CalibrationCoefficients.Add(deviceConfig.CoefA);
                CalibrationCoefficients.Add(deviceConfig.CoefB);
                CalibrationCoefficients.Add(deviceConfig.CoefC);
                CalibrationCoefficients.Add(deviceConfig.CoefD);
            }

            OnPropertyChanged(nameof(CalibrationStatus));
            OnPropertyChanged(nameof(StatusColor));
        }

        private void GetSetupSettings()
        {
            AvailableChannels.Clear();

            var devices = AppSettings.Default.Devices;

            if (devices != null && devices.Any())
            {
                foreach (var device in devices)
                {
                    AvailableChannels.Add(device.ChannelId);
                }
            }

            if (AvailableChannels.Any())
            {
                SelectedChannel = AvailableChannels.First();
                LoadCoefficientsForSelectedChannel();
            }
        }
    }
}
