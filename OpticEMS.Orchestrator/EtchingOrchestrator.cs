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
        private bool _isDemoMode = false;
        private bool _isPcaBusy = false;
        private DateTime _lastPcaAnalysisTime = DateTime.MinValue;
        private long _frameIndex = 0;

        private readonly IEtchingProcessService _endpointService;
        private readonly ICalibrationService _calibrationService;
        private readonly IWavelengthMapper _wavelengthMapper;
        private readonly IRecipeFileManager _recipeFileManager;
        private readonly ISettingsProvider _configureProvider;
        private readonly IExportManager _exportManager;

        private readonly DeviceProcessing _deviceProcessing;
        private PcaAnalysisHandler? _pcaHandler;

        public EtchingOrchestrator(
            int channelId,
            IEtchingProcessService endpointService,
            ICalibrationService calibrationService,
            IWavelengthMapper wavelengthMapper,
            IRecipeFileManager recipeFileManager,
            ISettingsProvider configureProvider,
            IExportManager exportManager)
        {
            ChannelId = channelId;

            _cancellationToken = new CancellationTokenSource();
            _cancellationTokenStart = new CancellationTokenSource();

            _deviceProcessing = new DeviceProcessing(ChannelId, configureProvider);

            _endpointService = endpointService;
            _calibrationService = calibrationService;
            _wavelengthMapper = wavelengthMapper;
            _recipeFileManager = recipeFileManager;
            _configureProvider = configureProvider;
            _exportManager = exportManager;

            RegisterMessages();

            Task.Run(() =>
            {
                _deviceProcessing.StartContinueScan(1, 1, _cancellationToken.Token);
            });
        }

        public int ChannelId { get; private set; }

        public Recipe? Recipe { get; private set; }

        public string ProcessStatus { get; private set; } = "Waiting start";

        public string PcaStatus { get; private set; } = "None";

        public bool IsDemoMode => _isDemoMode;

        public DeviceProcessing Device => _deviceProcessing;

        public void ToggleDemoMode()
        {
            _isDemoMode = !_isDemoMode;
        }

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

            WeakReferenceMessenger.Default.Send(new RecipeAppliedMessage(
                ChannelId, Recipe.Wavelengths,
                Recipe.WavelengthColors));
            
            UpdateInternalIndexes();
            _currentIntensities = new uint[Recipe.Wavelengths.Count];
            _isIndicesCorrected = false;

            _pcaHandler = new PcaAnalysisHandler(
                new PcaSpectrumAnalyzer(),
                recipe.Name,
                recipe.PcaMinTrainingSize,
                recipe.PcaComponents);
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

            _frameIndex = 0;
            _isRunning = true;
            _isPaused = false;
            _stopwatch.Restart();
            _startTime = DateTime.Now;
            _exportData.Clear();

            WeakReferenceMessenger.Default.Send(new ExportAvailabilityChangedMessage(ChannelId, false));

            WeakReferenceMessenger.Default.Send(new SetUpProcessChartMessage(
                ChannelId,
                Recipe.Wavelengths,
                Recipe.WavelengthColors));

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
                //_endpointService.Pause();
                ProcessStatus = "Paused";
            }
            else
            {
                _stopwatch.Start();
                //_endpointService.Resume();
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

            WeakReferenceMessenger.Default.Send(new ExportAvailabilityChangedMessage(ChannelId, true));

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

        public async Task ExecuteAnalyzingTrainingAsync()
        {
            if (_isRunning)
            {
                throw new Exception("Stop the process before training PCA.");
            }

            if (Recipe is null)
            {
                throw new Exception("Recipe is not selected.");
            }

            if (_pcaHandler == null)
            {
                throw new Exception("PCA handler is not initialized.");
            }

            _isPcaBusy = true;
            PcaStatus = "Training PCA...";

            try
            {
                var spectra = _fullSpectrumHistory
                    .Select(s => s.ToArray())
                    .ToArray();

                var result = await Task.Run(() => _pcaHandler.TryAutoTrain(spectra));

                PcaStatus = result.IsAnomaly
                    ? _pcaHandler.Status
                    : $"PCA Error | {result.Message}";

                _fullSpectrumHistory.Clear();
            }
            finally
            {
                _isPcaBusy = false;
            }
        }

        private async Task RunProcessLoopAsync(CancellationToken cancellationToken)
        {
            int intervalMs = Recipe?.DetectionWindowTime ?? 100;
            double intervalSec = intervalMs / 1000.0;

            long frameIndex = 0;
            long nextTick = Environment.TickCount64;

            while (!cancellationToken.IsCancellationRequested)
            {
                long now = Environment.TickCount64;
                long sleep = nextTick - now;

                if (sleep > 0)
                {
                    await Task.Delay((int)sleep, cancellationToken);
                }

                nextTick += intervalMs;

                double currentTime = frameIndex * intervalSec;
                double currentTimeMs = currentTime * 1000.0;
                frameIndex++;

                if (!_isPaused && _isRunning && Recipe != null)
                {
                    if (Recipe.PcaEnabled)
                    {
                        _pcaHandler.ProcessAsync(_fullSpectrumHistory.LastOrDefault());
                        PcaStatus = _pcaHandler.Status;
                    }
                    else
                    {
                        PcaStatus = "PCA disabled";
                    }

                    var endpointResult = _endpointService.Update(currentTimeMs);

                    uint[] intensitiesSnapshot = (uint[])_currentIntensities.Clone();
                    RecordDataForExport(intensitiesSnapshot, currentTime);

                    WeakReferenceMessenger.Default.Send(new ProcessStepUpdateMessage(
                        ChannelId,
                        endpointResult.Status,
                        currentTime,
                        intensitiesSnapshot));

                    WeakReferenceMessenger.Default.Send(new PcaStatusMessage(ChannelId, PcaStatus));

                    if (endpointResult.Status != ProcessStatus)
                    {
                        ProcessStatus = endpointResult.Status;
                    }

                    if (endpointResult.IsDetected)
                    {
                        await FinishProcessAsync(endpointResult.IsForced);
                        break;
                    }
                }

                long behind = Environment.TickCount64 - nextTick;
                if (behind > intervalMs)
                {
                    long skipped = behind / intervalMs;
                    frameIndex += skipped;
                    nextTick += skipped * intervalMs;
                }
            }
        }

        private async Task FinishProcessAsync(bool forced)
        {
            double endpointTime = _endpointService.DetectedAtSeconds;
            double overEtchTime = _endpointService.OverEtchDurationSeconds;
            double totalTime = _endpointService.TotalDurationSeconds;

            _endpointService.Stop();
            _endTime = DateTime.Now;

            string report = forced
                ? $"Process at channel {ChannelId + 1} forced to stop at {totalTime:F2}s (Max Time reached)."
                : $"Endpoint detected in channel {ChannelId + 1} at: {endpointTime:F2} s\n" +
                  $"Over-etch duration: {overEtchTime:F2} s\n" +
                  $"Total process time: {totalTime:F2} s";

            ProcessStatus = "Endpoint detected";

            WeakReferenceMessenger.Default.Send(new ProcessFinishedMessage(ChannelId, report, forced));

            StopProcessAsync();
        }

        public void ExportToCsv(string path, string channelName)
        {
            if (_exportData.Count == 0)
            {
                throw new Exception("No data to export.");
            }

            double endpointTime = _endpointService.DetectedAtSeconds;
            double overEtchDurationSeconds = _endpointService.OverEtchDurationSeconds;

            var overEtchStartTime = _startTime.AddSeconds(endpointTime);
            var overEtchEndTime = overEtchStartTime.AddSeconds(overEtchDurationSeconds);

            _exportManager.ExportAsTextFormat(
                path: path,
                startTime: _startTime,
                endTime: _endTime,
                overEtchStartTime: overEtchStartTime,
                overEtchEndTime: overEtchEndTime,
                recipeName: Recipe.Name,
                channelName: channelName,
                wavelengths: Recipe.Wavelengths,
                points: _exportData);
        }

        public void ExportToExcel(string path, string channelName)
        {
            if (_exportData.Count == 0)
            {
                throw new Exception("No data to export.");
            }

            double endpointTime = _endpointService.DetectedAtSeconds;
            double overEtchDurationSeconds = _endpointService.OverEtchDurationSeconds;

            var overEtchStartTime = _startTime.AddSeconds(endpointTime);
            var overEtchEndTime = overEtchStartTime.AddSeconds(overEtchDurationSeconds);

            _exportManager.ExportAsXLS(
                path: path,
                startTime: _startTime,
                endTime: _endTime,
                overEtchStartTime: overEtchStartTime,
                overEtchEndTime: overEtchEndTime,
                recipeName: Recipe.Name,
                channelName: channelName,
                wavelengths: Recipe.Wavelengths,
                points: _exportData);
        }

        public void ExportToTxt(string path, string channelName)
        {
            if (_exportData.Count == 0)
            {
                throw new Exception("No data to export.");
            }

            double endpointTime = _endpointService.DetectedAtSeconds;
            double overEtchDurationSeconds = _endpointService.OverEtchDurationSeconds;

            var overEtchStartTime = _startTime.AddSeconds(endpointTime);
            var overEtchEndTime = overEtchStartTime.AddSeconds(overEtchDurationSeconds);

            _exportManager.ExportAsTextFormat(
                path: path,
                startTime: _startTime,
                endTime: _endTime,
                overEtchStartTime: overEtchStartTime,
                overEtchEndTime: overEtchEndTime,
                recipeName: Recipe.Name,
                channelName: channelName,
                wavelengths: Recipe.Wavelengths,
                points: _exportData);
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

            CreateSpectrumSnapshot(intensities);
            UpdateSpectrumChart(intensities, wavelengths);
        }

        private void UpdateSpectrumChart(uint[] intensities, double[] wavelengths)
        {
            long currentMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (currentMs - _lastUiUpdateMs > 33)
            {
                _lastUiUpdateMs = currentMs;

                WeakReferenceMessenger.Default.Send(new LiveSpectrumDataMessage(ChannelId, wavelengths, intensities));
            }
        }

        private void CreateSpectrumSnapshot(uint[] intensities)
        {
            var elapsed = (DateTime.Now - _lastPcaAnalysisTime).TotalMilliseconds;

            if (_isRunning && !_isPaused && elapsed >= 1000)
            {
                _fullSpectrumHistory.Add(intensities.ToArray());
                _pcaHandler?.PushForTraining(intensities);

                _lastPcaAnalysisTime = DateTime.Now;
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

            try
            {
                _recipeFileManager.SaveRecipe((Recipe)Recipe);
            }
            catch (Exception exception)
            {
                // Logging
            }

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
