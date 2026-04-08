using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Devices;
using OpticEMS.MVVM.Models;
using OpticEMS.MVVM.View.Windows;
using OpticEMS.Notifications.Messages;
using OpticEMS.Services.Calibration;
using OpticEMS.Services.Dialogs;
using OpticEMS.Services.Etching;
using OpticEMS.Services.Export;
using OpticEMS.Services.Files;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class ChannelViewModel : ObservableObject, IDisposable
    {
        #region services

        private readonly IWavelengthMapper _wavelengthMapper;
        private readonly IRecipeFileManager _recipeFileManager;
        private readonly IDialogService _dialogService;
        private readonly IEtchingProcessService _endpointService;
        private readonly ISettingsProvider _configureProvider;
        private readonly IExportManager _exportManager;
        private readonly ICalibrationService _calibrationService;

        #endregion

        #region fields

        private readonly List<TimePoint> _exportData = new();
        private CancellationTokenSource _cancellationToken = new();
        private CancellationTokenSource _cancellationTokenStart = new();
        private bool _isRunning;
        private bool _isPaused;
        private bool _isIndicesCorrected;
        private readonly Stopwatch _stopwatch = new();
        private DateTime _startTime;
        private DateTime _endTime;
        private ApplicationMessageBox? _activeDialog;
        private double[] _calibrationCoefficients = Array.Empty<double>();
        private DeviceProcessing? _deviceProcessing;
        private long _lastUiUpdateMs = 0;
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

        public SpectralLinesCatalogViewModel SpectralLinesCatalogViewModel { get; }

        public ChannelDetailsViewModel ChannelDetailsViewModel { get; }

        #endregion

        #region props
        
        public int ChannelId { get; set; }

        public string ChannelName { get; private set; } = "Unknown";

        #endregion

        #region ctor
        public ChannelViewModel(int id, IWavelengthMapper wavelengthMapper, IDialogService dialogService,
            IEtchingProcessService endpointService, ISettingsProvider configureProvider, IExportManager exportManager,
            IRecipeFileManager recipeFileManager, ICalibrationService calibrationService, ISpectralLineRepository spectralLineRepository) 
        {
            _wavelengthMapper = wavelengthMapper;
            _dialogService = dialogService;
            _endpointService = endpointService;
            _configureProvider = configureProvider;
            _exportManager = exportManager;
            _recipeFileManager = recipeFileManager;
            _calibrationService = calibrationService;

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
            SpectralLinesCatalogViewModel = new SpectralLinesCatalogViewModel(
                ChannelId, spectralLineRepository, dialogService);
            ChannelDetailsViewModel = new ChannelDetailsViewModel(this);


            SpectrumChartViewModel.OnWavelengthMoved += () =>
            {
                UpdateInternalIndexes();
                SaveUpdatedWavelengths();
            };

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
            _startTime = DateTime.Now;
            _exportData.Clear();

            ProcessChartViewModel.SetUpModel(Recipe.Wavelengths, Recipe.WavelengthColors);

            _ = Task.Run(() => RunProcessLoopAsync(_cancellationTokenStart.Token));

            _endpointService.Start(Recipe, _currentIntensities);
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
                _endpointService.Pause();
                ProcessStatus = "Paused";
            }
            else
            {
                _stopwatch.Start();
                _endpointService.Resume();
            }

            if (_configureProvider.GetByChannelId(ChannelId)?.DeviceType == DeviceType.VirtualSpec) 
            {
                _deviceProcessing?.NotifyVirtualProcessPaused();
            }
        }

        [RelayCommand]
        public async Task StopProcessAsync()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _isPaused = false;
            _isIndicesCorrected = false;
            _stopwatch.Stop();
            _endpointService.Stop();
            _cancellationTokenStart.Cancel();

            if (_configureProvider.GetByChannelId(ChannelId)?.DeviceType == DeviceType.VirtualSpec)
            {
                _deviceProcessing?.NotifyVirtualProcessStopped();

                await Task.Delay(5000);

                StartProcessAsync();
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
                    double endpointTime = _endpointService.DetectedAtSeconds;
                    double overEtchDurationSeconds = _endpointService.OverEtchDurationSeconds;

                    var overEtchStartTime = _startTime.AddSeconds(endpointTime);
                    var overEtchEndTime = overEtchStartTime.AddSeconds(overEtchDurationSeconds);

                    _exportManager.ExportAsTextFormat(dialog.FileName, _startTime, _endTime, overEtchStartTime, overEtchEndTime,
                        Recipe.Name, ChannelName, Recipe.Wavelengths, _exportData);

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
                    double endpointTime = _endpointService.DetectedAtSeconds;
                    double overEtchDurationSeconds = _endpointService.OverEtchDurationSeconds;

                    var overEtchStartTime = _startTime.AddSeconds(endpointTime);
                    var overEtchEndTime = overEtchStartTime.AddSeconds(overEtchDurationSeconds);

                    _exportManager.ExportAsXLS(dialog.FileName, _startTime, _endTime, overEtchStartTime, overEtchEndTime,
                        Recipe.Name, ChannelName, Recipe.Wavelengths, _exportData);

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
                    double endpointTime = _endpointService.DetectedAtSeconds;
                    double overEtchDurationSeconds = _endpointService.OverEtchDurationSeconds;

                    var overEtchStartTime = _startTime.AddSeconds(endpointTime);
                    var overEtchEndTime = overEtchStartTime.AddSeconds(overEtchDurationSeconds);

                    _exportManager.ExportAsTextFormat(dialog.FileName, _startTime, _endTime, overEtchStartTime, overEtchEndTime,
                        Recipe.Name, ChannelName, Recipe.Wavelengths, _exportData);

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

        private async Task RunProcessLoopAsync(CancellationToken cancellationToken)
        {
            var periodicStopwatch = Stopwatch.StartNew();
            periodicStopwatch.Start();

            long targetNextTickMs = 0;
            int interval = Recipe.DetectionWindowTime;
            var overEtchStarted = false;

            while (!cancellationToken.IsCancellationRequested)
            {
                targetNextTickMs += interval;

                if (!_isPaused && _isRunning)
                {
                    var result = _endpointService.Update(_currentIntensities);

                    ProcessStatus = result.Status;

                    if (Application.Current?.Dispatcher is Dispatcher dispatcher)
                    {
                        await dispatcher.InvokeAsync(() =>
                        {
                            RecordDataForExport(_currentIntensities);
                            ProcessChartViewModel.UpdateTopPlot(_stopwatch.Elapsed, _currentIntensities);

                            if (result.Status.Contains("Over") || result.Status.Contains("Endpoint Detected"))
                            {
                                if (!overEtchStarted)
                                {
                                    ProcessChartViewModel.MarkEndpoint(_stopwatch.Elapsed);
                                    ProcessChartViewModel.StartOverEtchArea(_stopwatch.Elapsed);
                                    overEtchStarted = true;
                                }
                                else
                                {
                                    ProcessChartViewModel.UpdateOverEtchArea(_stopwatch.Elapsed);
                                }
                            }
                        }, DispatcherPriority.Render);
                    }

                    if (result.IsDetected)
                    {
                        FinishProcess(result.IsForced);
                        break;
                    }
                }

                long currentMs = periodicStopwatch.ElapsedMilliseconds;
                long sleepTime = targetNextTickMs - currentMs;

                if (sleepTime > 0)
                {
                    await Task.Delay((int)sleepTime, cancellationToken);
                }
            }
        }

        private void FinishProcess(bool forced)
        {
            double endpointTime = _endpointService.DetectedAtSeconds;
            double overEtchTime = _endpointService.OverEtchDurationSeconds;
            double totalTime = _endpointService.TotalDurationSeconds;

            _endpointService.Stop();

            string report = forced
                ? $"Process at channel {ChannelName} forced to stop at {totalTime:F2}s (Max Time reached)."
                : $"Endpoint detected in channel \n{ChannelName} at: {endpointTime:F2} s\n" +
                  $"Over-etch duration: {overEtchTime:F2} s\n" +
                  $"Total process time: {totalTime:F2} s";

            ProcessStatus = "Endpoint detected";

            StopProcessAsync();

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (forced)
                {
                    _dialogService.ShowError(report);
                }
                else
                {
                    _dialogService.ShowInformationWithAutoClose(report);
                }
            });
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
                if (_isRunning)
                {
                    _dialogService.ShowInformation("Cannot apply recipe while process is running. Please stop the process first.");

                    return;
                }

                Recipe = recipe;

                SpectrumChartViewModel.UpdateAnnotations(Recipe.Wavelengths, Recipe.WavelengthColors);

                UpdateInternalIndexes();
                _currentIntensities = new uint[Recipe.Wavelengths.Count];
                _isIndicesCorrected = false;

                _dialogService.ShowInformation($"Recipe '{Recipe.Name}' for channel {Recipe.Channel + 1} applied successfully.");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Failed to apply recipe: {ex.Message}");
            }
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

                UpdateInternalIntensities(message.Intensities, message.Wavelengths);
            });

            WeakReferenceMessenger.Default.Register<SpectralLineSelectionMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId == this.ChannelId)
                {
                    UpdateSpectrumAnnotations(message.Wavelength, (Color)ColorConverter.ConvertFromString(message.ColorHex));
                }
            });
        }

        private void HandleIncomingSpectrum(uint[] intensities, double[] wavelengths)
        {
            if (intensities == null || intensities.Length == 0)
            {
                return;
            }

            long currentMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (currentMs - _lastUiUpdateMs > 33)
            {
                _lastUiUpdateMs = currentMs;
                Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    SpectrumChartViewModel.UpdateChart(wavelengths, intensities);
                }, DispatcherPriority.Render);
            }
        }

        private void UpdateInternalIntensities(uint[] intensities, double[] wavelengths)
        {
            if (!_isIndicesCorrected && _isRunning && intensities.Length > 0 && Recipe.AutocalibrationEnabled)
            {
                CorrectIndices(intensities);
                return;
            }

            for (int i = 0; i < _wavelengthsIndices.Length; i++)
            {
                int idx = _wavelengthsIndices[i];
                _currentIntensities[i] = (idx >= 0 && idx < intensities.Length)
                    ? intensities[idx]
                    : 0;
            }
        }

        private void CorrectIndices(uint[] intensities)
        {
            if (_wavelengthsIndices.Length == 0)
            {
                return;
            }

            for (int i = 0; i < _wavelengthsIndices.Length; i++)
            {
                _calibrationService.CorrectWavelengthIndices(intensities, ref _wavelengthsIndices[i]);
            }

            _isIndicesCorrected = true;

            _currentIntensities = new uint[_wavelengthsIndices.Length];
            for (int i = 0; i < _wavelengthsIndices.Length; i++)
            {
                int idx = _wavelengthsIndices[i];
                _currentIntensities[i] = (idx >= 0 && idx < intensities.Length)
                    ? intensities[idx]
                    : 0;
            }

            SaveUpdatedWavelengths();
            SpectrumChartViewModel.UpdateAnnotations(Recipe.Wavelengths, Recipe.WavelengthColors);
        }

        private void UpdateInternalIndexes()
        {
            var targets = Recipe.Wavelengths;
            _wavelengthsIndices = new int[targets.Count];
            var currentWavelengths = _deviceProcessing?.Wavelengths ?? Array.Empty<double>();

            for (int i = 0; i < targets.Count; i++)
            {
                _wavelengthsIndices[i] = _wavelengthMapper.FindNearestIndex(currentWavelengths, targets[i]);
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

        private void SaveUpdatedWavelengths()
        {
            Recipe.Wavelengths.Clear();

            foreach (var wavelengthIndex in _wavelengthsIndices)
            {
                var wavelength = _wavelengthMapper.FindWavelengthByPixel((uint)wavelengthIndex, _calibrationCoefficients);

                double roundedWavelength = Math.Round(wavelength, 2);

                Recipe.Wavelengths.Add(roundedWavelength);
            }

            _recipeFileManager.SaveRecipe(Recipe);
        }

        private void UpdateSpectrumAnnotations(double wavelength, Color color)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var selectedCatalogLines = SpectralLinesCatalogViewModel.SpectralLines
                    .Where(line => line.IsSelected)
                    .ToList();

                var wavelengths = selectedCatalogLines.Select(l => l.Wavelength).ToList();
                var colors = selectedCatalogLines.Select(l => l.LineColor).ToList();

                if (Recipe != null)
                {
                    wavelengths.AddRange(Recipe.Wavelengths);
                    colors.AddRange(Recipe.WavelengthColors);
                }

                SpectrumChartViewModel.UpdateAnnotations(wavelengths, colors);
            }, DispatcherPriority.Render);
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
