using OpticEMS.MVVM.ViewModels;
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

namespace OpticEMS.MVVM.View.Windows
{
    /// <summary>
    /// Interaction logic for PasswordWindow.xaml
    /// </summary>
    public partial class PasswordWindow : Window
    {
        public PasswordWindow(PasswordDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.RequestClose += (result) =>
            {
                try
                {
                    this.DialogResult = result;
                }
                catch (InvalidOperationException)
                {
                    this.Close();
                }
            };

            Loaded += (s, e) => PassBox.Focus();
        }
    }
}
