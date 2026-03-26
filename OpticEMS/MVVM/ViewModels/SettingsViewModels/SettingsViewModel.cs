using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpticEMS.MVVM.ViewModels.SettingsViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty] 
        private object _currentViewModel;

        private readonly ChamberSettingsViewModel _chamberSettingsViewModel;
        private readonly CalibrationSettingsViewModel _calibrationSettingsViewModel;

        [RelayCommand]
        private void ShowCalibrationSettings() => CurrentViewModel = _calibrationSettingsViewModel;

        [RelayCommand]
        private void ShowChamberSettings() => CurrentViewModel = _chamberSettingsViewModel;

        public SettingsViewModel(ChamberSettingsViewModel chamberSettingsViewModel,
            CalibrationSettingsViewModel calibrationSettingsViewModel)
        {
            _calibrationSettingsViewModel = calibrationSettingsViewModel;
            _chamberSettingsViewModel = chamberSettingsViewModel;

            CurrentViewModel = _calibrationSettingsViewModel;
        }
    }
}
