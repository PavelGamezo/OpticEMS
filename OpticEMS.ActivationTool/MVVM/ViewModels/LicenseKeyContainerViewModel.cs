using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.ActivationTool.MVVM.Models;
using OpticEMS.Notifications.Messages;

namespace OpticEMS.ActivationTool.MVVM.ViewModels
{
    public partial class LicenseKeyContainerViewModel : ObservableObject
    {
        private readonly LicenseKeyModel _licenseKeyModel = new();

        [ObservableProperty]
        private string _generatedKey;

        public LicenseKeyContainerViewModel()
        {
            RegisterMessages();
        }

        [RelayCommand(CanExecute = nameof(CanCopy))]
        private void Copy()
        {
            _licenseKeyModel.CopyToClipboard();
        }

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<LicenseKeyGenerated>(this, (recipient, message) =>
            {
                _licenseKeyModel.LicenseKey = message.GeneratedKey;

                GeneratedKey = message.GeneratedKey;

                CopyCommand.NotifyCanExecuteChanged();
            });
        }

        private bool CanCopy() => !string.IsNullOrWhiteSpace(GeneratedKey);
    }
}
