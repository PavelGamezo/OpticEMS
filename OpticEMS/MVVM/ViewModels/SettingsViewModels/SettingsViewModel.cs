using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticEMS.MVVM.ViewModels.Activation;

namespace OpticEMS.MVVM.ViewModels.SettingsViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty] 
        private object _currentViewModel;

        public ChamberSettingsViewModel ChamberSettingsViewModel { get; }
        public CalibrationSettingsViewModel CalibrationSettingsViewModel { get; }
        public LicenseSettingsViewModel LicenseSettingsViewModel { get; }

        [RelayCommand]
        private void ShowCalibrationSettings()
        {
            Serilog.Log.Warning("SettingsViewModel: User requested to show calibration settings");
            CurrentViewModel = CalibrationSettingsViewModel;
        }

        [RelayCommand]
        private void ShowChamberSettings()
        {
            Serilog.Log.Warning("SettingsViewModel: User requested to show chamber settings");
            CurrentViewModel = ChamberSettingsViewModel;
        }

        [RelayCommand]
        private void ShowLicense()
        {
            Serilog.Log.Warning("SettingsViewModel: User requested to show license");
            CurrentViewModel = LicenseSettingsViewModel;
        }

        public SettingsViewModel(
            ChamberSettingsViewModel chamberSettingsViewModel,
            CalibrationSettingsViewModel calibrationSettingsViewModel,
            LicenseSettingsViewModel licenseSettingsViewModel)
        {
            try
            {
                CalibrationSettingsViewModel = calibrationSettingsViewModel;
                ChamberSettingsViewModel = chamberSettingsViewModel;
                LicenseSettingsViewModel = licenseSettingsViewModel;

                CurrentViewModel = CalibrationSettingsViewModel;
            }
            catch (Exception exception)
            {
                Serilog.Log.Fatal(exception, "SettingsViewModel: Critical failure during startup...");
            }
        }
    }
}
