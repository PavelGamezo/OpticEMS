using CommunityToolkit.Mvvm.ComponentModel;
using System.Resources;
using System.Windows;

namespace OpticEMS.ActivationTool.MVVM.Models
{
    public class LicenseKeyModel
    {
        private string _licenseKey;

        public string LicenseKey 
        {
            get => _licenseKey;
            set 
            {
                _licenseKey = value;
            }
        }

        public void CopyToClipboard()
        {
            if (!string.IsNullOrWhiteSpace(LicenseKey))
            {
                Clipboard.SetText(LicenseKey);
            }
            else
            {
                throw new ArgumentNullException(
                    nameof(License), 
                    "Error: Empty license key to copy. Can't get instance key!");
            }
        }
    }
}
