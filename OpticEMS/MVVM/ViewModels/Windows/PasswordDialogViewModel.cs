using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticEMS.Contracts.Services.Settings;
using System.Windows.Controls;

namespace OpticEMS.MVVM.ViewModels
{
    public partial class PasswordDialogViewModel : ObservableObject
    {
        private readonly ISettingsProvider _settingsProvider;

        public PasswordDialogViewModel(ISettingsProvider settingsProvider)
        {
            _settingsProvider = settingsProvider;
        }

        public event Action<bool>? RequestClose;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [RelayCommand]
        private void Verify(object? parameter)
        {
            if (parameter is PasswordBox passwordBox)
            {
                if (passwordBox.Password == _settingsProvider.EntrySecret)
                {
                    RequestClose?.Invoke(true);
                }
                else
                {
                    ErrorMessage = "Invalid access key";
                    passwordBox.Clear();
                    passwordBox.Focus();
                }
            }
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);
    }
}
