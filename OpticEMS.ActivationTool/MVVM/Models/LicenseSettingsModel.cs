using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.License.Handlers;
using System.Security;

namespace OpticEMS.ActivationTool.MVVM.Models
{
    public partial class LicenseSettingsModel : ObservableObject
    {
        public byte[] CertificatePrivateKeyData { set; private get; }

        public byte[] CertificatePublicKeyData { private get; set; }

        public SecureString CertificatePassword { set; private get; }

        public OpticEMS.License.Common.License License { get; set; }

        public string Generate()
        {
            if (CertificatePrivateKeyData is null)
            {
                throw new ArgumentNullException(
                    nameof(CertificatePrivateKeyData),
                    "Error: Invalid private certificate format.");
            }

            License.CreateDateTime = DateTime.UtcNow;
            License.ExpireDateTime = DateTime.UtcNow + TimeSpan.FromDays(365);

            var licenseKey = LicenseHandler.GenerateLicense(License, CertificatePrivateKeyData, CertificatePassword);

            return licenseKey;
        }
    }
}
