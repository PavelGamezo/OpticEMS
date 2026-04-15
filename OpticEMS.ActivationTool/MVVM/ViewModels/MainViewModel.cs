using CommunityToolkit.Mvvm.ComponentModel;

namespace OpticEMS.ActivationTool.MVVM.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ActivationSettingsViewModel _settingsViewModel;

        [ObservableProperty]
        private LicenseKeyContainerViewModel _licenseContainerViewModel;

        public MainViewModel(ActivationSettingsViewModel settingsViewModel,
            LicenseKeyContainerViewModel licenseContainerViewModel)
        {
            SettingsViewModel = settingsViewModel;
            LicenseContainerViewModel = licenseContainerViewModel;
        }
    }
}
