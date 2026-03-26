using System.Windows;
using System.Windows.Input;

namespace OpticEMS.Services.Windows
{
    public class WindowService : IWindowService
    {
        public void Close()
        {
            Application.Current.MainWindow.Close();
        }

        public void Move()
        {
            if (Application.Current.MainWindow != null &&
                Mouse.LeftButton == MouseButtonState.Pressed)
            {
                Application.Current.MainWindow.DragMove();
            }
        }

        public void MaximizeOrRestore()
        {
            if (Application.Current.MainWindow.WindowState == WindowState.Maximized)
            {
                Application.Current.MainWindow.WindowState = WindowState.Normal;
            }
            else
            {
                Application.Current.MainWindow.WindowState = WindowState.Maximized;
            }
        }

        public void Minimize()
        {
            Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }
    }
}
