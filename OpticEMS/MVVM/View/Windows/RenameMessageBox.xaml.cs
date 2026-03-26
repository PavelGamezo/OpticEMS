using OpticEMS.ViewModels;
using System.Windows;

namespace OpticEMS.MVVM.View.Windows
{
    public partial class RenameMessageBox : Window
    {
        public string ResultName { get; private set; } = string.Empty; 

        public RenameMessageBox(string currentName) 
        { 
            InitializeComponent(); 
            
            DataContext = new RenameDialogViewModel(currentName);
        }

        private void Yes_Click(object sender, RoutedEventArgs e) 
        { 
            ResultName = ((RenameDialogViewModel)DataContext).NewName; 
            
            DialogResult = true; Close(); 
        }

        private void No_Click(object sender, RoutedEventArgs e) 
        {
            DialogResult = false; Close();
        }
    }
}
