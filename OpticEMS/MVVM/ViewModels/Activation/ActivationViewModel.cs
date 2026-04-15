using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticEMS.License.Helpers;
using OpticEMS.MVVM.Models.Activation;
using OpticEMS.Services.Windows;
using System.Reflection;
using System.Windows;

namespace OpticEMS.MVVM.ViewModels.Activation
{
    public partial class ActivationViewModel : ObservableObject
    {
        private readonly IWindowService _windowService;

        [ObservableProperty]
        private string _uid;

        [ObservableProperty]
        private string _licenseKey;

        [ObservableProperty]
        private Version? _projectVersion;

        [ObservableProperty]
        private string _versionDisplayName;

        [ObservableProperty]
        private string _edition = "Commercial";

        [ObservableProperty]
        private string _instance;

        public ActivationViewModel(IWindowService windowService)
        {
            _windowService = windowService;

            ShowVersionInfo();
            ShowInstanceId();

            ShowUniqueId();
        }

        public ActivationModel ActivationModel = new ActivationModel();

        [RelayCommand]
        private void ApplyLicense()
        {
            ActivationModel.LicenseString = LicenseKey;

            if (ActivationModel.ValidateLicense())
            {
                ActivationModel.SaveLicense(AppDomain.CurrentDomain.BaseDirectory);

                MessageBox.Show("License accepted, the application will be closed. Please, restart the application before your work!",
                    string.Empty,
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);

                Application.Current.Shutdown();
            }
        }

        [RelayCommand]
        private void CopyToClipboardUid()
        {
            ActivationModel.CopyUid();
        }

        [RelayCommand]
        private void CopyToClipboardVersion()
        {
            ActivationModel.CopyVersion();
        }

        [RelayCommand]
        private void MoveWindow() => _windowService.Move();

        [RelayCommand]
        private void CloseWindow() => _windowService.Close();

        public void ShowUniqueId()
        {
            var uniqueId = LicenseHelper.GenerateUid();

            Uid = uniqueId;
            ActivationModel.Uid = uniqueId;
        }

        private void ShowVersionInfo()
        {
            _projectVersion = Assembly.GetExecutingAssembly().GetName().Version;
            VersionDisplayName = $"{ProjectVersion.Major}.{ProjectVersion.Minor}.{ProjectVersion.Build}";

            ActivationModel.Version = VersionDisplayName;
        }

        private void ShowInstanceId()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory.ToLower();
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(path));
                Instance = BitConverter.ToString(hash).Replace("-", "").Substring(0, 8).ToLower();
            }
        }

        public void ConveyCertificate(byte[] publicKey)
        {
            ActivationModel.CertificatePublicKeyData = publicKey;
        }
    }
}
