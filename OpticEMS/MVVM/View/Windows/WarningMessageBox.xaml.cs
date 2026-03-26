using System.Windows;

namespace OpticEMS.MVVM.View.Windows
{
    public partial class WarningMessageBox : Window
    {
        public WarningMessageBox(string message)
        {
            InitializeComponent();

            MessageTextBlock.Text = message;
        }

        public static bool? Show(string message, Window owner = null)
        {
            var msg = new WarningMessageBox(message);

            if (owner != null)
            {
                msg.Owner = owner;
            }

            return msg.ShowDialog();
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
