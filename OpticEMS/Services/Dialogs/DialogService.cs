using OpticEMS.Common.Enums;
using OpticEMS.MVVM.View.Windows;
using System.Windows;

namespace OpticEMS.Services.Dialogs
{
    public class DialogService : IDialogService
    {
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
    }
}
