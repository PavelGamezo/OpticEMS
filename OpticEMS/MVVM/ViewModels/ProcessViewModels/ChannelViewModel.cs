using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Devices;
using OpticEMS.Notifications.Messages;
using OpticEMS.Services.Calibration;
using OpticEMS.Services.Dialogs;
using OpticEMS.Services.Export;
using OpticEMS.MVVM.Models;
using OpticEMS.Services.Etching;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class ChannelViewModel : ObservableObject, IDisposable
    {
        #region services

        private readonly IWavelengthMapper _wavelengthMapper;
        private readonly IDialogService _dialogService;
        private readonly IEtchingProcessService _endpointService;
        private readonly ISettingsProvider _configureProvider;
        private readonly IExportManager _exportManager;

        #endregion

        #region fields

        private readonly List<TimePoint> _exportData = new();
        private CancellationTokenSource _cancellationToken = new();
        private CancellationTokenSource _cancellationTokenStart = new();
        private bool _isRunning;
        private bool _isEndpointReached; 
        private bool _isPaused;
        private readonly Stopwatch _stopwatch = new();
        private double[] _calibrationCoefficients = Array.Empty<double>();
        private DeviceProcessing? _deviceProcessing;
        private int[] _wavelengthsIndices = Array.Empty<int>();
        public uint[] _currentIntensities = Array.Empty<uint>();

        #endregion

        #region observable props

        [ObservableProperty] 
        private string _processStatus = "Waiting start";

        [ObservableProperty] 
        private RecipeModel? _recipe;

        [ObservableProperty] 
        private bool _endpointReached;

        #endregion

        #region viewModels

        public SpectrumChartViewModel SpectrumChartViewModel { get; }

        public ProcessChartViewModel ProcessChartViewModel { get; }

        #endregion

        #region props
        
        public int ChannelId { get; set; }

        public string ChannelName { get; private set; } = "Unknown";

        #endregion

        #region ctor
        public ChannelViewModel(int id,
            IWavelengthMapper wavelengthMapper,
            IDialogService dialogService,
            IEtchingProcessService endpointService,
            ISettingsProvider configureProvider,
            IExportManager exportManager) 
        {
            _wavelengthMapper = wavelengthMapper;
            _dialogService = dialogService;
            _endpointService = endpointService;
            _configureProvider = configureProvider;
            _exportManager = exportManager;

            _cancellationToken = new CancellationTokenSource();
            _cancellationTokenStart = new CancellationTokenSource();

            ChannelId = id;
            ChannelName = $"Chamber {id + 1}";

            _deviceProcessing = new DeviceProcessing(
                ChannelId,
                _configureProvider);

            RegisterMessages();
            LoadCalibration();

            SpectrumChartViewModel = new SpectrumChartViewModel();
            ProcessChartViewModel = new ProcessChartViewModel();

            Task.Run(() =>
            {
                _deviceProcessing.StartContinueScan(1, 1, _cancellationToken.Token);
            });
        }

        #endregion

        #region relayCommands

        [RelayCommand]
        private async Task StartProcessAsync()
        {
            if (Recipe is null)
            {
                _dialogService.ShowInformation("Please select a recipe before starting the process.");

                return;
            }

            if (_isRunning)
            {
                _dialogService.ShowInformation("Process is already running.");

                return;
            }

            if (_configureProvider.GetByChannelId(ChannelId).DeviceType == DeviceType.VirtualSpec)
            {
                _deviceProcessing.NotifyVirtualProcessStarted();
            }

            _cancellationTokenStart.Cancel();
            _cancellationTokenStart = new CancellationTokenSource();

            _isRunning = true;
            _isPaused = false;
            _stopwatch.Restart();
            _exportData.Clear();

            ProcessChartViewModel.SetUpModel(Recipe.Wavelengths, Recipe.WavelengthColors);

            _ = Task.Run(() => RunTopPlotLoopAsync(_cancellationTokenStart.Token));

            _endpointService.Start(Recipe, _currentIntensities.ToArray());
        }

        [RelayCommand]
        private void PauseProcess()
        {
            if (!_isRunning)
            {
                return;
            }

            _isPaused = !_isPaused;

            if (_isPaused)
            {
                _stopwatch.Stop();
                ProcessStatus = "Paused";
            }
            else
            {
                _stopwatch.Start();
            }

            if (_configureProvider.GetByChannelId(ChannelId)?.DeviceType == DeviceType.VirtualSpec) 
            {
                _deviceProcessing?.NotifyVirtualProcessPaused();
            }
        }

        [RelayCommand]
        public void StopProcess()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _isPaused = false;
            _isEndpointReached = false;
            _stopwatch.Stop();
            _cancellationTokenStart.Cancel();

            if (_configureProvider.GetByChannelId(ChannelId)?.DeviceType == DeviceType.VirtualSpec)
            {
                _deviceProcessing?.NotifyVirtualProcessPaused();
            }
        }

        [RelayCommand(CanExecute = nameof(IsExportEnabled))]
        public void ExportToCsv()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"Channel_{ChannelId + 1}_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    _exportManager.ExportAsTextFormat(dialog.FileName, Recipe.Name, ChannelName, Recipe.Wavelengths, _exportData);

                    _dialogService.ShowInformation("Data exported successfully.");
                }
            }
            catch (Exception exception)
            {
                _dialogService.ShowInformation(exception.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(IsExportEnabled))]
        public void ExportToExcel()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx",
                    FileName = $"Channel_{ChannelId + 1}_Data_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    _exportManager.ExportAsXLS(dialog.FileName, Recipe.Name, ChannelName, Recipe.Wavelengths, _exportData);

                    _dialogService.ShowInformation("Data exported successfully.");
                }
            }
            catch (Exception exception)
            {
                _dialogService.ShowError($"Failed to export data: {exception.Message}");
            }
        }


        [RelayCommand(CanExecute = nameof(IsExportEnabled))]
        public void ExportToTxt()
        {

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt",
                    FileName = $"Channel_{ChannelId + 1}_Data_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    _exportManager.ExportAsTextFormat(dialog.FileName, Recipe.Name, ChannelName, Recipe.Wavelengths, _exportData);

                    _dialogService.ShowInformation("Data exported successfully");
                }
            }
            catch (Exception exception)
            {
                _dialogService.ShowError($"Failed to export data: {exception.Message}");
            }
        }

        #endregion

        #region methods

        private async Task RunTopPlotLoopAsync(CancellationToken cancellationToken)
        {
            var periodicStopwatch = Stopwatch.StartNew();
            long targetNextTickMs = 0;
            int interval = Recipe.DetectionWindowTime;

            while (!cancellationToken.IsCancellationRequested)
            {
                targetNextTickMs += interval;

                if (_isPaused)
                {
                    await Task.Delay(Recipe?.DetectionWindowTime ?? 100, cancellationToken);
                    continue;
                }

                var elapsed = _stopwatch.Elapsed;

                if (Application.Current?.Dispatcher is Dispatcher dispatcher &&
                    !dispatcher.HasShutdownStarted &&
                    !dispatcher.HasShutdownFinished)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        RecordDataForExport(_currentIntensities);

                        ProcessChartViewModel.UpdateTopPlot(elapsed, _currentIntensities);
                    });
                }
                else
                {
                    break;
                }

                long currentMs = periodicStopwatch.ElapsedMilliseconds;
                long sleepTime = targetNextTickMs - currentMs;

                if (sleepTime > 0)
                {
                    await Task.Delay((int)sleepTime, cancellationToken);
                }
            }
        }

        private void LoadCalibration()
        {
            var device = _configureProvider.GetByChannelId(ChannelId);

            _calibrationCoefficients = device != null
                ? new[] { device.CoefA, device.CoefB, device.CoefC, device.CoefD }
                : new[] { 0.0, 0.0, 0.0, 0.0 };
        }

        public void ApplyRecipe(RecipeModel recipe)
        {
            try
            {
                if (!_isRunning)
                {
                    Recipe = recipe;

                    SpectrumChartViewModel.UpdateAnnotations(Recipe.Wavelengths, Recipe.WavelengthColors);

                    var targets = recipe.Wavelengths;
                    _wavelengthsIndices = new int[targets.Count];
                    var currentWavelengths = _deviceProcessing?.Wavelengths ?? Array.Empty<double>();

                    for (int i = 0; i < targets.Count; i++)
                    {
                        _wavelengthsIndices[i] = _wavelengthMapper.FindNearestIndex(currentWavelengths, targets[i]);
                    }

                    _currentIntensities = new uint[Recipe.Wavelengths.Count];

                    _dialogService.ShowInformation($"Recipe '{Recipe.Name}' for channel {Recipe.Channel + 1} applied successfully.");
                }
                else
                {
                    _dialogService.ShowInformation("Cannot apply recipe while process is running. Please stop the process first.");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Failed to apply recipe: {ex.Message}");
            }
        }

        private async void TriggerEndpoint(double elapsed, bool forced)
        {
            TimeSpan overEtch = TimeSpan.Zero;

            if (!forced && Recipe.OverEtchEnabled)
            {
                double overVal = Recipe.OverEtchValue;

                overEtch = TimeSpan.FromMilliseconds(overVal);
                string overEtchDisplay = overEtch.TotalSeconds.ToString("F1");

                ProcessStatus = $"Over-etching for {overEtchDisplay} seconds...";

                await Task.Delay(overEtch);
            }

            string message = forced
                ? "Force endpoint triggered."
                : $"Endpoint detected at {TimeSpan.FromMilliseconds(elapsed).TotalSeconds:F1}s.\n" +
                  $"Over-etch duration: {overEtch.TotalSeconds:F1}s";

            ProcessStatus = "Endpoint detected";

            StopProcess();

            Application.Current.Dispatcher.Invoke(() =>
            {
                _dialogService.ShowInformation(message);
            });
        }

        private bool IsExportEnabled() => _exportData.Count > 0 && !_isRunning;

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<SpectrumUpdatedMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId != this.ChannelId)
                {
                    return;
                }

                HandleIncomingSpectrum(message.Intensities, message.Wavelengths);
            });
        }

        private void HandleIncomingSpectrum(uint[] intensities, double[] wavelengths)
        {
            if (intensities == null || intensities.Length == 0)
            {
                return;
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                SpectrumChartViewModel.UpdateChart(wavelengths, intensities);
            }, DispatcherPriority.Render);

            _ = Task.Run(() => ProcessSpectrumData(intensities, wavelengths));
        }

        private void ProcessSpectrumData(uint[] data, double[] wavelengths)
        {
            if (Recipe is null || _isPaused)
            {
                return;
            }

            if (_wavelengthsIndices.Length == 0)
            {
                var targetWavelengths = Recipe.Wavelengths;
                _wavelengthsIndices = new int[targetWavelengths.Count];

                for (int i = 0; i < targetWavelengths.Count; i++)
                {
                    _wavelengthsIndices[i] = _wavelengthMapper.FindNearestIndex(wavelengths, targetWavelengths[i]);
                }
            }

            for (int i = 0; i < _wavelengthsIndices.Length; i++)
            {
                _currentIntensities[i] = data[_wavelengthsIndices[i]];
            }

            if (_isRunning && !_isEndpointReached && !_isPaused)
            {
                var elapsed = _stopwatch.Elapsed.TotalMilliseconds;
                var result = _endpointService.CheckEndpoint(_currentIntensities, elapsed);

                if (result.Status != ProcessStatus) 
                {
                    ProcessStatus = result.Status;
                }

                if (result.IsDetected)
                {
                    _isEndpointReached = true;
                    TriggerEndpoint(elapsed, result.IsForced);
                }
            }
        }

        private void RecordDataForExport(uint[] currentIntensities)
        {
            var timePoint = new TimePoint
            {
                TimeSeconds = _stopwatch.Elapsed.TotalSeconds,
                Intensities = new List<uint>(currentIntensities)
            };

            _exportData.Add(timePoint);
        }

        public void Dispose() 
        {
            _stopwatch.Stop();

            _cancellationToken.Cancel();
            _cancellationToken.Dispose();

            _cancellationTokenStart.Cancel();
            _cancellationTokenStart.Dispose();
        }

        #endregion
    }
}
