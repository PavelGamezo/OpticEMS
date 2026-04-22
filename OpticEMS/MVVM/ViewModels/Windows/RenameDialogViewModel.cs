using CommunityToolkit.Mvvm.ComponentModel;

namespace OpticEMS.ViewModels
{
    public partial class RenameDialogViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _newName = string.Empty;

        public RenameDialogViewModel(string newName)
        {
            _newName = newName;
        }
    }
}
