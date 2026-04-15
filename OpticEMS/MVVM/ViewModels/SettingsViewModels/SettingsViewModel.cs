using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticEMS.MVVM.ViewModels.Activation;

namespace OpticEMS.MVVM.ViewModels.SettingsViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty] 
        private object _currentViewModel;

        private readonly ChamberSettingsViewModel _chamberSettingsViewModel;
        private readonly CalibrationSettingsViewModel _calibrationSettingsViewModel;
        private readonly ActivationViewModel _activationViewModel;

        [RelayCommand]
        private void ShowCalibrationSettings() => CurrentViewModel = _calibrationSettingsViewModel;

        [RelayCommand]
        private void ShowChamberSettings() => CurrentViewModel = _chamberSettingsViewModel;

        [RelayCommand]
        private void ShowLicense() => CurrentViewModel = _activationViewModel;

        public SettingsViewModel(ChamberSettingsViewModel chamberSettingsViewModel,
            CalibrationSettingsViewModel calibrationSettingsViewModel,
            ActivationViewModel activationViewModel)
        {
            _calibrationSettingsViewModel = calibrationSettingsViewModel;
            _chamberSettingsViewModel = chamberSettingsViewModel;
            _activationViewModel = activationViewModel;

            CurrentViewModel = _calibrationSettingsViewModel;
        }
    }
}
