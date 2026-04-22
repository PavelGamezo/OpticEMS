using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Devices;
using OpticEMS.MVVM.Models;
using OpticEMS.MVVM.Models.Recipe;
using OpticEMS.Notifications.Messages;
using OpticEMS.Processing.PCA;
using OpticEMS.Services.Calibration;
using OpticEMS.Services.Dialogs;
using OpticEMS.Services.Etching;
using OpticEMS.Services.Export;
using OpticEMS.Services.Files;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using XAct;

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
        private readonly PcaSpectrumAnalyzer _analyzer;
        private readonly DeviceProcessing _deviceProcessing;

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
        private long _lastUiUpdateMs = 0;
        private int[] _wavelengthsIndices = Array.Empty<int>();
        public uint[] _currentIntensities = Array.Empty<uint>(); 
        private List<uint[]> _fullSpectrumHistory = new List<uint[]>();
        private const int MaxHistorySize = 150;
        private bool _isDemoMode = false; 
        private bool _isMonitoringAreaActive;
        private bool _isOverEtchAreaActive;

        #endregion

        #region observable props

        [ObservableProperty] 
        private string _processStatus = "Waiting start";

        [ObservableProperty]
        private string _pcaStatus = "None";

        [ObservableProperty] 
        private RecipeModel? _recipe;

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
            _analyzer = new PcaSpectrumAnalyzer();

            RegisterMessages();

            SpectrumChartViewModel = new SpectrumChartViewModel(
                _deviceProcessing.Device.DeviceInfo.TrimLeft,
                _deviceProcessing.Device.DeviceInfo.TrimRight);
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

        public ChannelViewModel()
        {
            
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
            _isMonitoringAreaActive = false;
            _isOverEtchAreaActive = false;
            _stopwatch.Stop();
            _endpointService.Stop();
            _cancellationTokenStart.Cancel();

            if (_configureProvider.GetByChannelId(ChannelId)?.DeviceType == DeviceType.VirtualSpec)
            {
                _deviceProcessing?.NotifyVirtualProcessStopped();

                if (_isDemoMode)
                {
                    await Task.Delay(5000);

                    StartProcessAsync();
                }
            }
        }

        [RelayCommand]
        public async Task ToggleDemoMode()
        {
            _isDemoMode = !_isDemoMode;
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
            int interval = Recipe?.DetectionWindowTime ?? 100;

            string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", $"{Recipe.Name}.pca");
            bool usePca = Recipe.PCAEnabled;

            while (!cancellationToken.IsCancellationRequested)
            {
                targetNextTickMs += interval;

                if (!_isPaused && _isRunning && Recipe != null)
                {
                    if (usePca)
                    {
                        RunPcaAnalysis(modelPath);
                    }
                    else
                    {
                        PcaStatus = "PCA disabled";
                    }

                    var result = _endpointService.Update(_currentIntensities);

                    if (result.Status != ProcessStatus)
                    {
                        ProcessStatus = result.Status;
                    }

                    await UpdateChartAreasAsync(result);

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
                else if (sleepTime < -50)
                {
                    targetNextTickMs = currentMs;
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

        private async Task UpdateChartAreasAsync(EndpointResult result)
        {
            if (Application.Current?.Dispatcher is Dispatcher dispatcher)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    RecordDataForExport(_currentIntensities);
                    ProcessChartViewModel.UpdateTopPlot(_stopwatch.Elapsed, _currentIntensities);

                    if (result.Status.Contains("Monitoring"))
                    {
                        if (!_isMonitoringAreaActive)
                        {
                            ProcessChartViewModel.MarkEndpointMonitoring(_stopwatch.Elapsed);
                            ProcessChartViewModel.StartEndpointMonitoringArea(_stopwatch.Elapsed);
                            _isMonitoringAreaActive = true;
                        }
                        else
                        {
                            ProcessChartViewModel.UpdateMonitoringArea(_stopwatch.Elapsed);
                        }
                    }
                    else if (result.Status.Contains("Over") || result.Status.Contains("Endpoint Detected"))
                    {
                        if (!_isOverEtchAreaActive)
                        {
                            ProcessChartViewModel.MarkEndpoint(_stopwatch.Elapsed);
                            ProcessChartViewModel.StartOverEtchArea(_stopwatch.Elapsed);
                            _isOverEtchAreaActive = true;
                        }
                        else
                        {
                            ProcessChartViewModel.UpdateOverEtchArea(_stopwatch.Elapsed);
                        }
                    }
                }, DispatcherPriority.Render);
            }
        }

        private void RunPcaAnalysis(string modelPath)
        {
            if (File.Exists(modelPath))
            {
                try
                {
                    _analyzer.LoadModel(modelPath);
                }
                catch
                {
                    /* Ошибка загрузки */
                }
            }
            if (!_analyzer.IsTrained && _exportData.Count >= 100)
            {
                var trainingData = _fullSpectrumHistory
                    .TakeLast(100);

                _analyzer.TryAutoTrain(trainingData, modelPath);
                PcaStatus = "Training";
            }

            if (_analyzer.IsTrained)
            {
                try
                {
                    var pcaResult = _analyzer.Analyze(_fullSpectrumHistory.Last());

                    PcaStatus = pcaResult.IsAnomaly
                        ? $"PCA ANOMALY → ${pcaResult.Message}"
                        : $"PCA Normal | T²={pcaResult.T2:F5}";
                }
                catch
                {
                    PcaStatus = "Error";
                }
            }
        }

        public void ApplyRecipe(RecipeModel recipe)
        {
            try
            {
                if (_isRunning)
                {
                    _dialogService.ShowError("Cannot apply recipe while process is running. Please stop the process first.");

                    return;
                }

                Recipe = recipe;

                _cancellationToken.Cancel();
                _cancellationToken = new CancellationTokenSource();
                Task.Run(() =>
                {
                    _deviceProcessing.StartContinueScan(recipe.ExposureMs, recipe.ScansNum, _cancellationToken.Token);
                });

                Task.Run(() =>
                {
                    SpectrumChartViewModel.UpdateAnnotations(Recipe.Wavelengths, Recipe.WavelengthColors);
                });

                UpdateInternalIndexes();
                _currentIntensities = new uint[Recipe.Wavelengths.Count];
                _isIndicesCorrected = false;
                _analyzer.NComponents = Recipe.PCAComponents;

                _dialogService.ShowInformation($"Recipe '{Recipe.Name}' for channel {Recipe.Channel} applied successfully.");
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

            _fullSpectrumHistory.Add((uint[])intensities.Clone());

            if (_fullSpectrumHistory.Count > MaxHistorySize)
            {
                _fullSpectrumHistory.RemoveAt(0);
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

            var calibrationCoefficients = new double[]
            {
                _deviceProcessing.Device.DeviceInfo.CoefA,
                _deviceProcessing.Device.DeviceInfo.CoefB,
                _deviceProcessing.Device.DeviceInfo.CoefC,
                _deviceProcessing.Device.DeviceInfo.CoefD,
            };

            foreach (var wavelengthIndex in _wavelengthsIndices)
            {
                var wavelength = _wavelengthMapper.FindWavelengthByPixel((uint)wavelengthIndex, calibrationCoefficients);

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
