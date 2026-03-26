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
        private bool _isCollapsed = false;
        private double _cachedWidth;

        public Process()
        {
            InitializeComponent();
        }

        private void BtnToggleMenu_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCollapsed)
            {
                _cachedWidth = SideMenuParent.ActualWidth;
                SideMenuColumn.Width = new GridLength(0);
                BtnToggleMenu.Content = "◀";
            }
            else
            {
                SideMenuColumn.Width = new GridLength(_cachedWidth);
                BtnToggleMenu.Content = "▶";
            }

            _isCollapsed = !_isCollapsed;
        }
    }
}
