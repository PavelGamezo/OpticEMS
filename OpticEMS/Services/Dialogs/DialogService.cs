using Microsoft.Extensions.DependencyInjection;
using OpticEMS.Common.Enums;
using OpticEMS.MVVM.View.Windows;
using OpticEMS.MVVM.ViewModels;
using System.Windows;

namespace OpticEMS.Services.Dialogs
{
    public class DialogService : IDialogService
    {
        private readonly IServiceProvider _serviceProvider;

        public DialogService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private Window Owner => Application.Current.MainWindow;

        public void ShowError(string message)
        {
            ApplicationMessageBox.Show(message, null, Owner, MessageType.Error);
        }

        public void ShowInformation(string message)
        {
            ApplicationMessageBox.Show(message, null, Owner, MessageType.Info);
        }

        public bool AskWarningQuestion(string message)
        {
            return WarningMessageBox.Show(message, Owner) == true;
        }

        public string? ShowRenameQuestion(string currentName)
        {
            var dialog = new RenameMessageBox(currentName)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.ResultName;
            }

            return string.Empty;
        }

        public void ShowInformationWithAutoClose(string message, int timeoutMs = 4500)
        {
            ApplicationMessageBox.ShowWithAutoClose(message, null, Owner, MessageType.Info);
        }

        public bool AskPassword()
        {
            var viewModel = _serviceProvider.GetRequiredService<PasswordDialogViewModel>();

            var window = new PasswordWindow(viewModel)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            return window.ShowDialog() ?? false;
        }
    }
}
