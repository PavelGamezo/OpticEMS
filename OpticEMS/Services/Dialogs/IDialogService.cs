namespace OpticEMS.Services.Dialogs
{
    public interface IDialogService
    {
        void ShowError(string message);

        bool AskWarningQuestion(string message);

        bool AskPassword();

        void ShowInformation(string message);

        string? ShowRenameQuestion(string currentName);

        void ShowInformationWithAutoClose(string message, int timeoutMs = 3000);
    }
}
