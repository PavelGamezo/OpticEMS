using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Communication.Modules;
using OpticEMS.Contracts.Services.Calibration;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Contracts.Services.Etching;
using OpticEMS.Contracts.Services.Export;
using OpticEMS.Contracts.Services.Mapper;
using OpticEMS.Contracts.Services.Recipe;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Devices;
using OpticEMS.Notifications.Messages;
using OpticEMS.Preprocessing;
using OpticEMS.Preprocessing.Modes;
using OpticEMS.Processing;
using OpticEMS.Processing.PCA;
using Serilog;
using System.Buffers;
using System.Diagnostics;

namespace OpticEMS.Orchestrator
{
    public class EtchingOrchestrator : IDisposable
    {
        private const double CALIBRATION_INTERVAL_MS = 5000;

        private readonly List<TimePoint> _exportData = new();
        private CancellationTokenSource _cancellationToken = new();
        private CancellationTokenSource _cancellationTokenStart = new();
        private bool _isRunning;
        private bool _isPaused;
        private bool _wavelengthChanged;
        private readonly Stopwatch _stopwatch = new();
        private DateTime _startTime;
        private DateTime _endTime;
        private long _lastUiUpdateMs = 0;
        private int[] _wavelengthsIndices = Array.Empty<int>();
        public double[] _currentIntensities = Array.Empty<double>(); 
        private readonly List<double[]> _fullSpectrumHistory = new();
        private bool _isPcaBusy = false;
        private DateTime _lastPcaAnalysisTime = DateTime.MinValue;
        private DateTime _lastAutocalibrationTime = DateTime.MinValue;

        private readonly IEtchingProcessService _endpointService;
        private readonly IRecipeRepository _recipeRepository;
        private readonly ICalibrationService _calibrationService;
        private readonly IWavelengthMapper _wavelengthMapper;
        private readonly ISettingsProvider _configureProvider;
        private readonly IExportManager _exportManager;

        private readonly DeviceProcessing _deviceProcessing;
        private PcaAnalysisHandler? _pcaHandler;
        private ModuleHandler _connectionHandler;
        private ModePreprocessorHandler _modeHandler;
        private TrendEquationsHandler _trendHandler;

        public EtchingOrchestrator(
            int channelId,
            IEtchingProcessService endpointService,
            IRecipeRepository recipeRepository,
            ICalibrationService calibrationService,
            IWavelengthMapper wavelengthMapper,
            ISettingsProvider configureProvider,
            IExportManager exportManager)
        {
            ChannelId = channelId;

            _cancellationToken = new CancellationTokenSource();
            _cancellationTokenStart = new CancellationTokenSource();

            var ip = configureProvider?.GetByChannelId(ChannelId)?.Ip;
            var port = int.Parse(configureProvider?.GetByChannelId(ChannelId).Port);

            _deviceProcessing = new DeviceProcessing(ChannelId, configureProvider);
            //_connectionHandler = new ModuleHandler(ip, port);

            _endpointService = endpointService;
            _recipeRepository = recipeRepository;
            _calibrationService = calibrationService;
            _wavelengthMapper = wavelengthMapper;
            _configureProvider = configureProvider;
            _exportManager = exportManager;

            RegisterMessages();

            Task.Run(() =>
            {
                _deviceProcessing.StartContinueScan(1, 1, _cancellationToken.Token);
            });

            //_connectionHandler.OnInputChanged += HandleModuleInputs;
        }

        public int ChannelId { get; private set; }

        public Recipe? Recipe { get; private set; }

        public string ProcessStatus { get; private set; } = "Waiting start";

        public string PcaStatus { get; private set; } = "None";

        public DeviceProcessing Device => _deviceProcessing;

        public async Task ApplyRecipe(Recipe recipe)
        {
            if (_isRunning)
            {
                throw new Exception("Cannot apply recipe while process is running. Please stop the process first.");
            }

            Recipe = recipe;
            _modeHandler = new ModePreprocessorHandler(Recipe);
            _trendHandler = new TrendEquationsHandler(Recipe.DerivativeEnabled);
            _trendHandler.Set(Recipe.MagneticFieldPeriodMs, Recipe.FieldPeriodsToAverage, Recipe.DerivativePoints);

            WeakReferenceMessenger.Default.Send(new RecipeAppliedMessage(
                ChannelId, Recipe.Wavelengths,
                Recipe.WavelengthColors));

            UpdateInternalIndexes();
            _currentIntensities = new double[Recipe.Wavelengths.Count];
            _wavelengthChanged = false;

            _pcaHandler = new PcaAnalysisHandler(
                new PcaSpectrumAnalyzer(),
                Recipe.Name,
                Recipe.PcaMinTrainingSize,
                Recipe.PcaComponents);
        }

        public void ApplySpecParams(float exposureMs, int scansNum)
        {
            _cancellationToken.Cancel();
            _cancellationToken = new CancellationTokenSource();

            _ = Task.Run(() =>
            {
                _deviceProcessing.StartContinueScan(exposureMs, scansNum, _cancellationToken.Token);
            });
        }

        public async Task ApplyRecipe(int recipeId)
        {
            if (_isRunning)
            {
                throw new Exception("Cannot apply recipe while process is running. Please stop the process first.");
            }

            Recipe = await _recipeRepository.GetRecipeByRecipeIdAsync(recipeId);
            _modeHandler = new ModePreprocessorHandler(Recipe);
            _trendHandler = new TrendEquationsHandler(Recipe.DerivativeEnabled);

            WeakReferenceMessenger.Default.Send(new RecipeAppliedMessage(
                ChannelId, Recipe.Wavelengths,
                Recipe.WavelengthColors));
            
            UpdateInternalIndexes();
            _currentIntensities = new double[Recipe.Wavelengths.Count];
            _wavelengthChanged = false;

            _pcaHandler = new PcaAnalysisHandler(
                new PcaSpectrumAnalyzer(),
                Recipe.Name,
                Recipe.PcaMinTrainingSize,
                Recipe.PcaComponents);
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

            _endpointService.ClearConfirmedWindowsIn();
            _endpointService.ClearConfirmedWindowsOut();

            _isRunning = true;
            _isPaused = false;
            _stopwatch.Restart();
            _startTime = DateTime.Now;
            _exportData.Clear();
            _fullSpectrumHistory.Clear();

            WeakReferenceMessenger.Default.Send(new ExportAvailabilityChangedMessage(ChannelId, false));

            WeakReferenceMessenger.Default.Send(new SetUpProcessChartMessage(
                ChannelId,
                Recipe.Wavelengths,
                Recipe.WavelengthColors));

            _ = Task.Run(() => RunProcessLoopAsync(_cancellationTokenStart.Token));

            _endpointService.Start(Recipe, _currentIntensities);

            Log.Information("Process START requested. Recipe: {RecipeName}, Device: {DeviceType}",
                Recipe?.Name, _configureProvider.GetByChannelId(ChannelId)?.DeviceType);
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

        public async Task StopProcessAsync()
        {
            if (!_isRunning)
            {
                throw new Exception("Process is not running.");
            }

            //_connectionHandler.SetOutputs((false, false, true, false));

            _isRunning = false;
            _isPaused = false;
            _stopwatch.Stop();
            _endpointService.Stop();
            _endpointService.ClearConfirmedWindowsIn();
            _endpointService.ClearConfirmedWindowsOut();
            _cancellationTokenStart.Cancel();
            _trendHandler.Reset();

            WeakReferenceMessenger.Default.Send(new ExportAvailabilityChangedMessage(ChannelId, true));

            if (_wavelengthChanged)
            {
                await SaveUpdatedWavelengthsAsync();
                _wavelengthChanged = false;
            }

            //WeakReferenceMessenger.Default.Send(new RecipeAppliedMessage(ChannelId, Recipe.Wavelengths, Recipe.WavelengthColors));

            if (_configureProvider.GetByChannelId(ChannelId)?.DeviceType == DeviceType.VirtualSpec)
            {
                _deviceProcessing?.NotifyVirtualProcessStopped();
            }

            if (Recipe.PcaEnabled)
            {
                await ExecuteAnalyzingTrainingAsync();
            }

            Log.Information("Process STOPPED. Duration: {Duration}s", 
                _stopwatch.Elapsed.TotalSeconds);
        }

        private async Task ExecuteAnalyzingTrainingAsync()
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

            Log.Information("[ORCHESTRATOR]: PCA Training started for recipe {RecipeName}. History size: {Count}",
                Recipe.Name, _fullSpectrumHistory.Count);

            double[][] spectra = null;

            try
            {
                spectra = _fullSpectrumHistory
                    .Select(spec =>
                    {
                        var copy = ArrayPool<double>.Shared.Rent(spec.Length);
                        Buffer.BlockCopy(spec, 0, copy, 0, spec.Length * sizeof(double));
                        return copy;
                    })
                    .ToArray();

                var result = await Task.Run(() => _pcaHandler.TryAutoTrain(spectra));

                PcaStatus = result.IsAnomaly
                    ? _pcaHandler.Status
                    : $"PCA Error | {result.Message}";

                Log.Information("[ORCHESTRATOR]: PCA Training completed");
            }
            catch(Exception exception)
            {
                Log.Error(exception, "[ORCHESTRATOR]: Critical error during PCA training");
                throw;
            }
            finally
            {
                if (spectra != null)
                {
                    foreach (var spec in spectra)
                    {
                        ArrayPool<double>.Shared.Return(spec);
                    }
                }

                foreach (var s in _fullSpectrumHistory)
                {
                    ArrayPool<double>.Shared.Return(s);
                }

                _fullSpectrumHistory.Clear();
                _isPcaBusy = false;
            }
        }

        private async Task RunProcessLoopAsync(CancellationToken cancellationToken)
        {
            const int intervalMs = 33;
            long nextTick = Environment.TickCount64;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (await WaitForNextTickAsync(nextTick, cancellationToken))
                {
                    break;
                }

                nextTick += intervalMs;

                if (ShouldProcess())
                {
                    await ExecuteProcessStepAsync();
                }

                nextTick = AdjustForLag(nextTick, intervalMs);
            }
        }

        private async Task<bool> WaitForNextTickAsync(long nextTick, CancellationToken ct)
        {
            long now = Environment.TickCount64;
            long sleep = nextTick - now;

            if (sleep > 0)
            {
                try
                {
                    await Task.Delay((int)sleep, ct);
                }
                catch (OperationCanceledException)
                {
                    Log.Information("[ORCHESTRATOR]: Operation cancellation requested");
                    return true;
                }
            }
            return false;
        }

        private bool ShouldProcess()
        {
            return !_isPaused && _isRunning && Recipe != null;
        }

        private long AdjustForLag(long nextTick, int intervalMs)
        {
            long now = Environment.TickCount64;
            long behind = now - nextTick;

            if (behind > intervalMs)
            {
                long skipped = behind / intervalMs;
                return nextTick + (skipped * intervalMs);
            }

            return nextTick;
        }

        private void BroadcastWindowBounds()
        {
            var windowBounds = _endpointService.GetCurrentWindowBounds();
            var confirmedWindowsIn = _endpointService.GetConfirmedWindowsIn();
            var confirmedWindowsOut = _endpointService.GetConfirmedWindowsOut();

            WeakReferenceMessenger.Default.Send(new DrawWindowBoundsMessage(
                ChannelId, 
                windowBounds,
                confirmedWindowsIn,
                confirmedWindowsOut));
        }

        private async Task ExecuteProcessStepAsync()
        {
            double currentTimeMs = _stopwatch.Elapsed.TotalMilliseconds;
            double currentTimeSec = currentTimeMs / 1000.0;

            BroadcastWindowBounds();

            double[] signal = PrepareSignal(currentTimeMs, currentTimeSec);

            var endpointResult = _endpointService.Update(signal, currentTimeMs);
            UpdateStatusAndNotify(endpointResult, signal, currentTimeSec);

            if (Recipe.PcaEnabled)
            {
                await ProcessPcaAnalyticsAsync();
            }

            if (endpointResult.IsDetected)
            {
                await FinishProcessAsync(endpointResult.IsForced);
            }
        }


        private double[] PrepareSignal(double currentTimeMs, double currentTimeSec)
        {
            var trendResult = _trendHandler.Process(currentTimeMs);

            double[] averagedSignal = trendResult.FrameAveraged;

            double[] preprocessedSignal = Recipe.DerivativeEnabled
                ? trendResult.Derivatives
                : trendResult.Smoothed;

            var processedSignal = _modeHandler.Process(preprocessedSignal);

            RecordDataForExport(averagedSignal, preprocessedSignal, processedSignal, currentTimeSec);

            return processedSignal;
        }

        private async Task ProcessPcaAnalyticsAsync()
        {
            var latestFullSpectrum = _fullSpectrumHistory.LastOrDefault();

            if (latestFullSpectrum == null || _pcaHandler == null)
            {
                return;
            }

            var result = await _pcaHandler.ProcessAsync(latestFullSpectrum);

            if (PcaStatus != _pcaHandler.Status)
            {
                PcaStatus = _pcaHandler.Status;
                HandlePcaResult(result);
            }
        }

        private void HandlePcaResult(Result result)
        {
            var ranges = (result is PcaAnomalyResult detailed && result.IsAnomaly)
                ? _pcaHandler.DetectAnomalyRanges(detailed.Residual)
                : new List<(int, int)>();

            WeakReferenceMessenger.Default.Send(new PcaAnomalyMapMessage(ChannelId, ranges));
        }

        private void UpdateStatusAndNotify(EndpointResult endpointResult, double[] signal, double timeSec)
        {
            if (endpointResult.Status != ProcessStatus)
            {
                ProcessStatus = endpointResult.Status;
            }

            WeakReferenceMessenger.Default.Send(new ProcessStepUpdateMessage(
                ChannelId, endpointResult.Status, timeSec, signal));
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

            await StopProcessAsync();

            WeakReferenceMessenger.Default.Send(new ProcessFinishedMessage(ChannelId, report, forced));
        }

        private void HandleModuleInputs((int, bool) state)
        {
            var (recipeId, isStarted) = state;

            if (isStarted)
            {
                ApplyRecipe(recipeId);
                StartProcess();
            }
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

            //ReleaseExportBuffers();
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

            //ReleaseExportBuffers();
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

            //ReleaseExportBuffers();
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

        private void HandleIncomingSpectrum(double[] intensities, double[] wavelengths)
        {
            if (intensities == null || intensities.Length == 0)
            {
                return;
            }

            CreateSpectrumSnapshot(intensities);
            UpdateSpectrumChart(intensities, wavelengths);
        }

        private void UpdateSpectrumChart(double[] intensities, double[] wavelengths)
        {
            long currentMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (currentMs - _lastUiUpdateMs > 33)
            {
                _lastUiUpdateMs = currentMs;

                WeakReferenceMessenger.Default.Send(new LiveSpectrumDataMessage(ChannelId, wavelengths, intensities));
            }
        }

        private void CreateSpectrumSnapshot(double[] intensities)
        {
            var elapsed = (DateTime.Now - _lastPcaAnalysisTime).TotalMilliseconds;

            if (_isRunning && !_isPaused && elapsed >= 1000)
            {
                var rented = ArrayPool<double>.Shared.Rent(intensities.Length);
                Buffer.BlockCopy(intensities, 0, rented, 0, intensities.Length * sizeof(double));

                _fullSpectrumHistory.Add(rented);
                _pcaHandler?.PushForTraining(intensities);

                _lastPcaAnalysisTime = DateTime.Now;
            }
        }

        private void UpdateInternalIntensities(double[] intensities, double[] wavelengths)
        {
            if (_isRunning && intensities.Length > 0 && Recipe.AutocalibrationEnabled)
            {
                var elapsed = (DateTime.Now - _lastAutocalibrationTime).TotalMilliseconds;
                if (elapsed >= CALIBRATION_INTERVAL_MS)
                {
                    CorrectIndices(intensities);
                    _lastAutocalibrationTime = DateTime.Now;
                    return;
                }
            }

            for (int i = 0; i < _wavelengthsIndices.Length; i++)
            {
                int idx = _wavelengthsIndices[i];
                _currentIntensities[i] = (idx >= 0 && idx < intensities.Length) ? intensities[idx] : 0;
            }

            if (_isRunning && !_isPaused)
            {
                _trendHandler.PushIntensities(_currentIntensities);
            }
        }

        private void CorrectIndices(double[] intensities)
        {
            if (_wavelengthsIndices.Length == 0)
            {
                return;
            }

            for (int i = 0; i < _wavelengthsIndices.Length; i++)
            {
                int oldIndex = _wavelengthsIndices[i];
                _calibrationService.CorrectWavelengthIndices(intensities, ref _wavelengthsIndices[i]);

                if (oldIndex != _wavelengthsIndices[i])
                {
                    _wavelengthChanged = true;
                }
            }

            _currentIntensities = new double[_wavelengthsIndices.Length];
            for (int i = 0; i < _wavelengthsIndices.Length; i++)
            {
                int idx = _wavelengthsIndices[i];
                _currentIntensities[i] = (idx >= 0 && idx < intensities.Length) ? intensities[idx] : 0;
            }
        }

        public async Task UpdateWavelengthManually()
        {
            Log.Information("[ORCHESTRATOR]: Wavelength update requested");

            UpdateInternalIndexes();
            await SaveUpdatedWavelengthsAsync();
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

        private async Task SaveUpdatedWavelengthsAsync()
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
                var wavelength = _wavelengthMapper.FindWavelengthByPixel(wavelengthIndex, calibrationCoefficients);

                double roundedWavelength = Math.Round(wavelength, 2);

                Recipe.Wavelengths.Add(roundedWavelength);
            }

            try
            {
                await _recipeRepository.UpdateRecipeAsync(Recipe);
                await _recipeRepository.SaveChangesAsync();

                Log.Information("[ORCHESTRATOR]: Recipe {RecipeName} wavelengths updated in database",
                    Recipe.Name);
            }
            catch (Exception exception)
            {
                Log.Error("[ORCHESTRATOR]: Failed to save updated wavelength to database for {Name} with ID {RecipeId}",
                    Recipe.Name, Recipe.DatabaseId);
            }

            WeakReferenceMessenger.Default.Send(
                new RecipeAppliedMessage(ChannelId, Recipe.Wavelengths, Recipe.WavelengthColors));
        }

        #region export

        private void RecordProcessedDataForExport(double[] trend, double currentTime)
        {
            if (Recipe.CanUseCombinedMode || Recipe.CanUseRatioMode)
            {
                var processedTimePoint = new TimePoint
                {
                    TimeSeconds = currentTime,
                    Trend = trend
                };

                _exportData.Add(processedTimePoint);
            }
        }

        private void RecordDataForExport(
            double[] averaged,
            double[] preprocessed,
            double[] processed,
            double currentTime)
        {
            var timePoint = new TimePoint
            {
                TimeSeconds = currentTime,
                Trend = averaged,
                Preprocessed = preprocessed,
                Processed = processed
            };
            
            _exportData.Add(timePoint);
        }

        /*
        private void ReleaseExportBuffers()
        {
            foreach (var tp in _exportData)
            {
                ArrayPool<double>.Shared.Return(tp.Intensities);
            }

            _exportData.Clear();
        }*/

        #endregion

        #region disposing

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                try
                {
                    if (_isRunning)
                    {
                        _ = StopProcessAsync();
                    }

                    _cancellationToken?.Cancel();

                    Task.Delay(300).Wait(500);

                    _deviceProcessing?.Dispose();

                    _trendHandler?.Reset();
                    _pcaHandler = null;

                    WeakReferenceMessenger.Default.UnregisterAll(this);

                    _exportData.Clear();
                    _fullSpectrumHistory.Clear();

                    _endpointService?.ClearConfirmedWindowsIn();
                    _endpointService?.ClearConfirmedWindowsOut();
                }
                catch (Exception ex)
                {
                    Log.Error($"[ORCHESTRATOR]: Error in Orchestrator.Dispose: {ex.Message}");
                }
            }

            _cancellationToken?.Dispose();
            _cancellationTokenStart?.Dispose();

            _disposed = true;
        }

        ~EtchingOrchestrator()
        {
            Dispose(false);
        }

        #endregion
    }
}
