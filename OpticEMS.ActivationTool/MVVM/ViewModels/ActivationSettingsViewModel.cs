using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.ActivationTool.MVVM.Models;
using OpticEMS.License.Common;
using OpticEMS.Notifications.Messages;
using System.IO;
using System.Reflection;
using System.Security;
using System.Windows;

namespace OpticEMS.ActivationTool.MVVM.ViewModels
{
    public partial class ActivationSettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateSecretKeyCommand))]
        private string _hwid;

        [ObservableProperty]
        private DateTime _expiryDate = DateTime.Now.AddYears(1);
        
        [ObservableProperty]
        private int _channelCount;

        public ActivationSettingsViewModel()
        {
            LoadSertificate();
        }

        public LicenseSettingsModel LicenseSettings { get; private set; }

        [RelayCommand(CanExecute = nameof(CanGenerate))]
        private void GenerateSecretKey()
        {
            try
            {
                LicenseSettings.License.Uid = Hwid;
                LicenseSettings.License.ChannelCount = ChannelCount;
                var licenseKey = LicenseSettings.Generate();

                WeakReferenceMessenger.Default.Send(new LicenseKeyGenerated(licenseKey));
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        private void LoadSertificate()
        {
            using (var certPwd = new SecureString())
            {
                var assembly = Assembly.GetExecutingAssembly();

                byte[] certPubicKeyData;
                using (var mem = new MemoryStream())
                {
                    assembly.GetManifestResourceStream(assembly.GetName().Name + ".LicenseSign.pfx")?.CopyTo(mem);
                    certPubicKeyData = mem.ToArray();
                }

                certPwd.AppendChar('2');
                certPwd.AppendChar('0');
                certPwd.AppendChar('1');
                certPwd.AppendChar('7');
                certPwd.AppendChar('o');
                certPwd.AppendChar('c');
                certPwd.AppendChar('p');
                certPwd.AppendChar('b');
                certPwd.AppendChar('b');

                LicenseSettings = new LicenseSettingsModel
                {
                    CertificatePrivateKeyData = certPubicKeyData,
                    CertificatePassword = certPwd.Copy(),
                    License = new OpticEMSLicense()
                };
            }
        }

        private bool CanGenerate() => !string.IsNullOrWhiteSpace(Hwid);
    }
}
