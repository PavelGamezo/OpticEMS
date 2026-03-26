namespace OpticEMS.Services.Dialogs
{
    public interface IDialogService
    {
        void ShowError(string message);

        bool AskWarningQuestion(string message);

        void ShowInformation(string message);

        string? ShowRenameQuestion(string currentName);
    }
}
