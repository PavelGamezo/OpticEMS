using Microsoft.Win32;
using OpticEMS.MVVM.ViewModels.SettingsViewModels;
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

namespace OpticEMS.MVVM.View.Settings
{
    /// <summary>
    /// Interaction logic for UpdateDialog.xaml
    /// </summary>
    public partial class UpdateDialog : UserControl
    {
        private UpdateViewModel ViewModel => DataContext as UpdateViewModel;

        public UpdateDialog()
        {
            InitializeComponent();
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    ProcessSelectedFile(files[0]);
                }
            }
        }

        private void Border_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Border_DragLeave(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "\r\nUpdate archive (*.zip)|*.zip",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ProcessSelectedFile(openFileDialog.FileName);
            }
        }

        private void ProcessSelectedFile(string filePath)
        {
            if (ViewModel != null && System.IO.Path.GetExtension(filePath).ToLower() == ".zip")
            {
                ViewModel.UpdateFilePath(filePath);
            }
        }
    }
}
