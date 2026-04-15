using OpticEMS.MVVM.ViewModels.Activation;
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
using System.Windows.Shapes;

namespace OpticEMS.MVVM.View.Activation
{
    /// <summary>
    /// Interaction logic for ActivationWindow.xaml
    /// </summary>
    public partial class ActivationWindow : Window
    {
        public ActivationWindow(ActivationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
