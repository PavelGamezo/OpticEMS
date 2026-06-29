using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace OpticEMS.MVVM.ViewModels.SettingsViewModels
{
    public partial class UpdateViewModel : ObservableObject
    {
        private const string UpdateExeName = "OpticEMS.Updater.exe";
        private const string MainExeName = "OpticEMS.exe";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartUpdateCommand))]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _selectedFileName = "Drag and drop archive or click to select";

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public UpdateViewModel()
        {
        }

        public event Action<bool>? RequestClose;

        public void UpdateFilePath(string path)
        {
            if (IsProcessing) return;

            FilePath = path;
            SelectedFileName = Path.GetFileName(path);
            StatusMessage = $"Selected file: {SelectedFileName}";
            Log.Information("[UPDATE_VM]: Selected update package: {Path}", path);
        }

        [RelayCommand]
        private void StartUpdate()
        {
            IsProcessing = true;
            StatusMessage = "Preparing update files...";
            Log.Information("[UPDATE_VM]: Starting update process from archive: {Path}", FilePath);

            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                string tempDir = Path.Combine(Path.GetTempPath(), "OpticEMS_Update_Files").TrimEnd('\\', '/');

                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                StatusMessage = "Unpacking the archive...";
                ZipFile.ExtractToDirectory(FilePath, tempDir);
                Log.Information("[UPDATE_VM]: Extracted to {TempDir}", tempDir);

                string updaterSource = Path.Combine(appDir, UpdateExeName);
                string updaterTarget = Path.Combine(tempDir, UpdateExeName);

                if (!File.Exists(updaterSource))
                    throw new FileNotFoundException($"{UpdateExeName} not found in app directory.");

                File.Copy(updaterSource, updaterTarget, true);
                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterTarget,
                    Arguments = $"\"{tempDir}\" \"{appDir}\" \"{MainExeName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                StatusMessage = "Starting the update...";
                Log.Information("[UPDATE_VM]: updaterTarget path = '{Path}'", updaterTarget);
                Log.Information("[UPDATE_VM]: File exists = {Exists}", File.Exists(updaterTarget));
                Log.Information("[UPDATE_VM]: Arguments = '{Args}'", $"\"{tempDir}\" \"{appDir}\" \"{MainExeName}\"");

                var updaterProcess = Process.Start(startInfo);

                Log.Information("[UPDATE_VM]: Process.Start returned. Process is null = {IsNull}", updaterProcess == null);
                if (updaterProcess == null)
                {
                    IsProcessing = false;
                    StatusMessage = "Update cancelled.";
                    Log.Warning("[UPDATE_VM]: Updater process did not start (UAC cancelled?)");
                    return;
                }

                Log.Information("[UPDATE_VM]: Updater launched. PID={Pid}. Shutting down.", updaterProcess.Id);
                RequestClose?.Invoke(true);
                Application.Current.Shutdown();
            }
            catch (System.ComponentModel.Win32Exception win32ex) when (win32ex.NativeErrorCode == 1223)
            {
                IsProcessing = false;
                StatusMessage = "Update cancelled by user.";
                Log.Warning("[UPDATE_VM]: UAC prompt cancelled by user");
            }
            catch (Exception exception)
            {
                IsProcessing = false;
                StatusMessage = "Error during update process.";
                Log.Error(exception, "[UPDATE_VM]: Failed to prepare update");

                MessageBox.Show(
                    $"Unable to prepare update:\n{exception.Message}",
                    "Update error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);
    }
}
