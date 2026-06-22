using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OpticEMS.MVVM.View.Process
{
    /// <summary>
    /// Interaction logic for Process.xaml
    /// </summary>
    public partial class Process : Page
    {
        public Process()
        {
            InitializeComponent();

            dockingManager.Loaded += DockingManager_Loaded;
        }

        private void DockingManager_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dockingManager.Layout == null || dockingManager.Layout.RootPanel == null)
                {
                    dockingManager.Layout = new AvalonDock.Layout.LayoutRoot();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AvalonDock init error: " + ex);
            }
        }
    }
}
