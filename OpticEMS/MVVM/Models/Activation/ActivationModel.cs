using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.License.Common;
using OpticEMS.License.Handlers;
using System.IO;
using System.Windows;

namespace OpticEMS.MVVM.Models.Activation
{
    public class ActivationModel : ObservableObject
    {
        private OpticEMS.License.Common.License _license;
        private string _licenseString;
        private string _version;
        private string _uid;

        public ActivationModel()
        {
            _uid = LicenseHandler.GenerateUid();
        }

        public string Uid 
        {
            get => _uid;
            set { _uid = value; }
        }

        public string Version
        {
            get => _version;
            set { _version = value; }
        }

        public string LicenseString
        {
            get => _licenseString;
            set { _licenseString = value; }
        }

        public byte[] CertificatePublicKeyData { private get; set; }
        
        private string LicenseBase64String => _licenseString.Trim();

        public bool ValidateLicense()
        {
            if (string.IsNullOrWhiteSpace(_licenseString))
            {
                MessageBox.Show(@"License UID is blank or invalid", 
                    string.Empty, 
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            LicenseHandler.ParseLicenseFromBase64String(
                _licenseString.Trim(),
                CertificatePublicKeyData, 
                out LicenseStatus licStatus, 
                out string message);
            
            switch (licStatus)
            {
                case LicenseStatus.Valid:
                    return true;
                default:
                    return false;
            }
        }

        public void SaveLicense(string path)
        {
            File.WriteAllText(Path.Combine(path, "license.lic"), LicenseBase64String);
        }

        public void CopyUid()
        {
            Clipboard.SetText(_uid);
        }

        public void CopyVersion()
        {
            Clipboard.SetText(_version);
        }
    }
}
