using OpticEMS.Common.Enums;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpticEMS.MVVM.View.Windows
{
    public partial class ApplicationMessageBox : Window
    {
        public MessageType MessageType { get; }

        public ApplicationMessageBox(string message, string details = null, MessageType messageType = MessageType.Info)
        {
            InitializeComponent();
            MessageTextBlock.Text = message;

            if (!string.IsNullOrEmpty(details))
            {
                DetailsTextBox.Text = details;
                DetailsExpander.Visibility = Visibility.Visible;
                CopyButton.Visibility = Visibility.Visible;
            }

            MessageType = messageType;
        }

        public static bool? Show(string message,
            string details = null,
            Window owner = null,
            MessageType type = MessageType.Info)
        {
            var msg = new ApplicationMessageBox(message, details);
            if (owner != null) msg.Owner = owner;

            switch (type)
            {
                case MessageType.Error:
                    msg.IconTextBlock.Text = "\uEA39";
                    msg.IconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(244, 135, 113));
                    break;
                case MessageType.Info:
                    msg.IconTextBlock.Text = "\uF167"; 
                    msg.IconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(55, 148, 255));
                    break;
            }

            return msg.ShowDialog();
        }

        public static void ShowWithAutoClose(string message,
            string details = null,
            Window owner = null,
            MessageType type = MessageType.Info,
            int timeoutMs = 4500)
        {
            var msg = new ApplicationMessageBox(message, details, type);

            if (owner != null)
            {
                msg.Owner = owner;
            }

            switch (type)
            {
                case MessageType.Error:
                    msg.IconTextBlock.Text = "\uEA39";
                    msg.IconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(244, 135, 113));
                    break;
                case MessageType.Info:
                    msg.IconTextBlock.Text = "\uF167";
                    msg.IconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(55, 148, 255));
                    break;
            }

            if (timeoutMs > 0)
            {
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(timeoutMs)
                };

                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    if (msg.DialogResult == null)
                    {
                        msg.DialogResult = true;
                        msg.Close();
                    }
                };

                timer.Start();
            }

            msg.ShowDialog();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText($"{MessageTextBlock.Text}\n\nDetails:\n{DetailsTextBox.Text}");

            var btn = (sender as Button);
            btn.Content = "Copied!";
            await Task.Delay(1500);
            btn.Content = "Copy Details";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
