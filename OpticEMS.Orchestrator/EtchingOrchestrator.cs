using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services.Calibration;
using OpticEMS.Contracts.Services.Etching;
using OpticEMS.Contracts.Services.Export;
using OpticEMS.Contracts.Services.Mapper;
using OpticEMS.Contracts.Services.Recipe;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Devices;
using OpticEMS.Notifications.Messages;
using OpticEMS.Processing.PCA;
using System.Diagnostics;

namespace OpticEMS.Orchestrator
{
    public class EtchingOrchestrator
    {
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

        private readonly DeviceProcessing _deviceProcessing;
        private readonly IEtchingProcessService _endpointService;
        private readonly ICalibrationService _calibrationService;
        private readonly IWavelengthMapper _wavelengthMapper;
        private readonly IRecipeFileManager _recipeFileManager;
        private readonly ISettingsProvider _configureProvider;
        private readonly PcaSpectrumAnalyzer _analyzer;

        public EtchingOrchestrator(
            int channelId,
            DeviceProcessing deviceProcessing,
            IEtchingProcessService endpointService,
            ICalibrationService calibrationService,
            IWavelengthMapper wavelengthMapper,
            IRecipeFileManager recipeFileManager,
            ISettingsProvider configureProvider,
            PcaSpectrumAnalyzer analyzer)
        {
            ChannelId = channelId;

            _deviceProcessing = deviceProcessing;
            _endpointService = endpointService;
            _calibrationService = calibrationService;
            _wavelengthMapper = wavelengthMapper;
            _recipeFileManager = recipeFileManager;
            _configureProvider = configureProvider;
            _analyzer = analyzer;

            RegisterMessages();
        }

        public int ChannelId { get; private set; }

        public Recipe Recipe { get; private set; }

        public bool IsRunning => _isRunning;

        public bool IsPaused => _isPaused;

        public string ProcessStatus { get; private set; } = "Waiting start";

        public string PcaStatus { get; private set; } = "None";

        public Stopwatch Stopwatch => _stopwatch;

        public void ApplyRecipe(Recipe recipe)
        {
            if (_isRunning)
            {
                throw new Exception("Cannot apply recipe while process is running. Please stop the process first.");
            }

            Recipe = recipe;

            _cancellationToken.Cancel();
            _cancellationToken = new CancellationTokenSource();
                
            Task.Run(() =>
            {
                _deviceProcessing.StartContinueScan(recipe.ExposureMs, recipe.ScansNum, _cancellationToken.Token);
            });

            WeakReferenceMessenger.Default.Send(new RecipeAppliedMessage(ChannelId, Recipe.Wavelengths, Recipe.WavelengthColors));
            //TODO: ChannelViewModel accepts this message and creates action
            //Task.Run((Action)(() =>
            //{
            //    SpectrumChartViewModel.UpdateAnnotations((IList<double>)Recipe.Wavelengths, (IReadOnlyList<Color>)Recipe.WavelengthColors);
            //}));

            UpdateInternalIndexes();
            _currentIntensities = new uint[Recipe.Wavelengths.Count];
            _isIndicesCorrected = false;
            _analyzer.NComponents = Recipe.PCAComponents;
                
            // And then use IDialogService for user notification in ChannelViewModel
        }

        public void StartProcess()
        {
            if (Recipe == null)
            {
                throw new Exception("Recipe is not selected. Please select a recipe before starting the process.");
            }

            if (_isRunning)
            {
                throw new Exception("Process is already running.");
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

            WeakReferenceMessenger.Default.Send(new SetUpProcessChartMessage(Recipe.Wavelengths, Recipe.WavelengthColors));

            _ = Task.Run(() => RunProcessLoopAsync(_cancellationTokenStart.Token));

            _endpointService.Start(Recipe, _currentIntensities);
        }

        public void PauseProcess()
        {
            if (!_isRunning)
            {
                throw new Exception("Process is not running.");
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

        public async Task StopProcessAsync()
        {
            if (!_isRunning)
            {
                throw new Exception("Process is not running.");
            }

            _isRunning = false;
            _isPaused = false;
            _isIndicesCorrected = false;
            _stopwatch.Stop();
            _endpointService.Stop();
            _cancellationTokenStart.Cancel();

            WeakReferenceMessenger.Default.Send(new ProcessStoppedMessage(ChannelId));

            if (_configureProvider.GetByChannelId(ChannelId)?.DeviceType == DeviceType.VirtualSpec)
            {
                _deviceProcessing?.NotifyVirtualProcessStopped();

                if (_isDemoMode)
                {
                    await Task.Delay(5000);

                    StartProcess();
                }
            }
        }

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

                    var result = _endpointService.Update();

                    var currentTime = _stopwatch.Elapsed.TotalSeconds;
                    uint[] intensitiesSnapshot = (uint[])_currentIntensities.Clone();

                    RecordDataForExport(intensitiesSnapshot, currentTime);

                    WeakReferenceMessenger.Default.Send(new ProcessStepUpdateMessage(
                        ChannelId,
                        result.Status,
                        currentTime,
                        intensitiesSnapshot));

                    if (result.Status != ProcessStatus)
                    {
                        ProcessStatus = result.Status;
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

            _isRunning = false;
            _endpointService.Stop();
            _endTime = DateTime.Now;

            string report = forced
                ? $"Process at channel {ChannelId + 1} forced to stop at {totalTime:F2}s (Max Time reached)."
                : $"Endpoint detected in channel {ChannelId + 1} at: {endpointTime:F2} s\n" +
                  $"Over-etch duration: {overEtchTime:F2} s\n" +
                  $"Total process time: {totalTime:F2} s";

            ProcessStatus = "Endpoint detected";

            // Send this message to the ViewModel to show process report
            //Application.Current.Dispatcher.Invoke(() =>
            //{
            //    if (forced)
            //    {
            //        _dialogService.ShowError(report);
            //    }
            //    else
            //    {
            //        _dialogService.ShowInformationWithAutoClose(report);
            //    }
            //});
            // AND DONT FORGET ABOUT StopProcessAsync()!!!!!!
            WeakReferenceMessenger.Default.Send(new ProcessFinishedMessage(ChannelId, report, forced));
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
                // TODO: ChannelViewModel accepts this message and creates action
                // Application.Current?.Dispatcher?.InvokeAsync(() =>
                //{
                //    SpectrumChartViewModel.UpdateChart(wavelengths, intensities);
                //}, DispatcherPriority.Render);
                WeakReferenceMessenger.Default.Send(new LiveSpectrumDataMessage(ChannelId, wavelengths, intensities));
            }
        }

        private void UpdateInternalIntensities(uint[] intensities, double[] wavelengths)
        {
            if (!_isIndicesCorrected && _isRunning &&
                intensities.Length > 0 && Recipe.AutocalibrationEnabled)
            {
                CorrectIndices(intensities);
                return;
            }

            for (int i = 0; i < _wavelengthsIndices.Length; i++)
            {
                int idx = _wavelengthsIndices[i];
                _currentIntensities[i] = (idx >= 0 && idx < intensities.Length) ? intensities[idx] : 0;
            }

            if (_isRunning && !_isPaused)
            {
                _endpointService.PushIntensities(_currentIntensities);
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
                _currentIntensities[i] = (idx >= 0 && idx < intensities.Length) ? intensities[idx] : 0;
            }

            SaveUpdatedWavelengths();

            // TODO: ChannelViewModel should handle this message and update new annotations
            // SpectrumChartViewModel.UpdateAnnotations((IList<double>)Recipe.Wavelengths, (IReadOnlyList<Color>)Recipe.WavelengthColors);
            WeakReferenceMessenger.Default.Send(new RecipeAppliedMessage(ChannelId, Recipe.Wavelengths, Recipe.WavelengthColors));
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

        public void UpdateWavelengthManually()
        {
            UpdateInternalIndexes();
            SaveUpdatedWavelengths();
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

            _recipeFileManager.SaveRecipe((Recipe)Recipe);

            WeakReferenceMessenger.Default.Send(
                new RecipeAppliedMessage(ChannelId, Recipe.Wavelengths, Recipe.WavelengthColors));
        }

        #region export

        private void RecordDataForExport(uint[] currentIntensities, double currentTime)
        {
            var timePoint = new TimePoint
            {
                TimeSeconds = currentTime,
                Intensities = new List<uint>(currentIntensities)
            };

            _exportData.Add(timePoint);
        }

        public List<TimePoint> GetExportData() => _exportData.ToList();

        #endregion
    }
}
