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
using OpticEMS.Preprocessing.Operations.Averaging;
using OpticEMS.Processing;
using OpticEMS.Processing.PCA;
using Serilog;
using System.Buffers;
using System.Diagnostics;

namespace OpticEMS.Orchestrator
{
    public class EtchingOrchestrator : IDisposable
    {
        private const double CALIBRATION_INTERVAL_MS = 1000;

        private Task _scanningTask = Task.CompletedTask;
        private CancellationTokenSource _cancellationToken = new();
        private CancellationTokenSource _cancellationTokenStart = new();
        private bool _isRunning;
        private bool _wavelengthChanged;
        private readonly Stopwatch _stopwatch = new();
        private DateTime _startTime;
        private DateTime _endTime;
        private float _exposureMs;
        private int _scansNum;
        private float _equalizer;
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

        private readonly DeviceProcessing _deviceProcessing;
        private PcaAnalysisHandler? _pcaHandler;
        private ModuleHandler _connectionHandler;
        private FrameAverager _frameAverager;
        private PipelineExecutor _pipelineExecutor;
        private ModePreprocessingHandler _modeHandler;

        public EtchingOrchestrator(
            int channelId,
            IEtchingProcessService endpointService,
            IRecipeRepository recipeRepository,
            ICalibrationService calibrationService,
            IWavelengthMapper wavelengthMapper,
            ISettingsProvider configureProvider)
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

            RegisterMessages();

            LoadSpectrometerParameters();
            _scanningTask = Task.Run(() =>
            {
                _deviceProcessing.StartContinueScan(ExposureMs, ScansNum, Equalizer, _cancellationToken.Token);
            });

            //_connectionHandler.OnInputChanged += HandleModuleInputs;
        }

        public int ChannelId { get; private set; }
        public bool IsRunning => _isRunning;
        public Recipe? Recipe { get; private set; }
        public float ExposureMs => _exposureMs;
        public int ScansNum => _scansNum;
        public float Equalizer => _equalizer;
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

            _modeHandler = new ModePreprocessingHandler(Recipe);
            _pipelineExecutor = new PipelineExecutor(recipe.DerivativeEnabled);
            _pipelineExecutor.Set(
                Recipe.MagneticFieldPeriodMs,
                Recipe.FieldPeriodsToAverage,
                Recipe.DerivativePoints);

            WeakReferenceMessenger.Default.Send(
                new RecipeAppliedMessage(ChannelId, Recipe.Wavelengths, Recipe.WavelengthColors));

            UpdateInternalIndexes();
            _currentIntensities = new double[Recipe.Wavelengths.Count];
            _wavelengthChanged = false;

            _pcaHandler = new PcaAnalysisHandler(
                new PcaSpectrumAnalyzer(),
                Recipe.Name,
                Recipe.PcaMinTrainingSize,
                Recipe.PcaComponents);
        }

        public async Task StartSpectrometerScan(float exposureMs, int scansNum, float equalizer)
        {
            try
            {
                Log.Information("[ORCHESTRATOR]: Start spectrometer scan request...");

                _cancellationToken.Cancel();

                try
                {
                    await _scanningTask;
                }
                catch (Exception exception)
                {
                    Log.Warning("[ORCHESTRATOR]: Scanning request awaiting cancelled.");
                }

                _cancellationToken = new CancellationTokenSource();
                _scanningTask = Task.Run(() =>
                {
                    _deviceProcessing.StartContinueScan(exposureMs, scansNum, equalizer, _cancellationToken.Token);
                }, _cancellationToken.Token);

                Log.Information("[ORCHESTRATOR]: Start spectrometer scan request completed.");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[ORCHESTRATOR]: Error changing spectrometer parameters.");
                _deviceProcessing.StopScanning();
            }
        }

        public async Task StopSpectrometerScan()
        {
            try
            {
                Log.Information("[ORCHESTRATOR]: Start spectrometer stop scanning request...");
                _cancellationToken.Cancel();

                try
                {
                    await _scanningTask;
                    _deviceProcessing.StopScanning();
                }
                catch (Exception exception)
                {
                    Log.Warning("[ORCHESTRATOR]: Scanning request awaiting cancelled.");
                }

                Log.Information("[ORCHESTRATOR]: Scanning request cancelled by the request.");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[ORCHESTRATOR]: Error stopping spectrometer scanning.");
                _deviceProcessing.StopScanning();
            }
        }

        public async Task ApplyRecipe(int recipeId)
        {
            if (_isRunning)
            {
                throw new Exception("Cannot apply recipe while process is running. Please stop the process first.");
            }

            Recipe = await _recipeRepository.GetRecipeByRecipeIdAsync(recipeId);

            _modeHandler = new ModePreprocessingHandler(Recipe);
            _pipelineExecutor = new PipelineExecutor(Recipe.DerivativeEnabled);
            _pipelineExecutor.Set(
                Recipe.MagneticFieldPeriodMs,
                Recipe.FieldPeriodsToAverage,
                Recipe.DerivativePoints);

            WeakReferenceMessenger.Default.Send(
                new RecipeAppliedMessage(ChannelId, Recipe.Wavelengths, Recipe.WavelengthColors));

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
            _stopwatch.Restart();
            _startTime = DateTime.Now;
            _fullSpectrumHistory.Clear();

            WeakReferenceMessenger.Default.Send(new ExportAvailabilityChangedMessage(ChannelId, false));

            WeakReferenceMessenger.Default.Send(new SetUpProcessChartMessage(
                ChannelId,
                Recipe.Wavelengths,
                Recipe.WavelengthColors));

            double[] fullyProcessedSignal = PrepareSignal(_stopwatch.Elapsed.TotalMilliseconds);
            while (fullyProcessedSignal.Length == 0)
            {
                fullyProcessedSignal = PrepareSignal(_stopwatch.Elapsed.TotalMilliseconds);
            }

            _endpointService.Start(Recipe, fullyProcessedSignal);
            _ = Task.Run(() => RunProcessLoopAsync(_cancellationTokenStart.Token));

            Log.Information("Process START requested. Recipe: {RecipeName}, Device: {DeviceType}",
                Recipe?.Name, _configureProvider.GetByChannelId(ChannelId)?.DeviceType);
        }

        public async Task StopProcessAsync()
        {
            if (!_isRunning)
            {
                throw new Exception("Process is not running.");
            }

            //_connectionHandler.SetOutputs((false, false, true, false));

            _isRunning = false;
            _stopwatch.Stop();
            _endpointService.Stop();
            _endpointService.ClearConfirmedWindowsIn();
            _endpointService.ClearConfirmedWindowsOut();
            _cancellationTokenStart.Cancel();

            _pipelineExecutor.Reset();

            WeakReferenceMessenger.Default.Send(new ExportAvailabilityChangedMessage(ChannelId, true));

            if (_wavelengthChanged)
            {
                await SaveUpdatedWavelengthsAsync();
                _wavelengthChanged = false;
            }

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
            catch (Exception exception)
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
            return _isRunning && Recipe != null;
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

            double[] signal = PrepareSignal(currentTimeMs);

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

        private double[] PrepareSignal(double currentTimeMs)
        {
            var trendResult = _pipelineExecutor.Process(currentTimeMs);
            double[] averagedSignal = trendResult.FrameAveraged;
            double[] preprocessedSignal = Recipe.DerivativeEnabled
                ? trendResult.Derivatives
                : trendResult.Smoothed;

            var processedSignal = _modeHandler.Process(preprocessedSignal);

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

        public ExportData GetExportData()
        {
            double endpointTime = _endpointService.DetectedAtSeconds;
            double overEtchDurationSeconds = _endpointService.OverEtchDurationSeconds;

            var overEtchStartTime = _startTime.AddSeconds(endpointTime);
            var overEtchEndTime = overEtchStartTime.AddSeconds(overEtchDurationSeconds);

            var result = new ExportData(_startTime, _endTime, overEtchStartTime, overEtchEndTime);

            return result;
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

            if (_isRunning && elapsed >= 1000)
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

            if (_isRunning)
            {
                _pipelineExecutor.PushIntensities(_currentIntensities);
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

        public async Task UpdateWavelengthManually(int wavelengthIndex, double newWavelength)
        {
            if (Recipe is null)
            {
                return;
            }

            if (_isRunning)
            {
                UpdateInternalIndexes();
            }
            else
            {
                Log.Information("[ORCHESTRATOR]: Wavelength update requested");
                Recipe.Wavelengths[wavelengthIndex] = Math.Round(newWavelength, 2);

                var currentWavelengths = _deviceProcessing?.Wavelengths ?? Array.Empty<double>();
                _wavelengthsIndices[wavelengthIndex] = _wavelengthMapper.FindNearestIndex(currentWavelengths, newWavelength);
            }

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

        private void LoadSpectrometerParameters()
        {
            var device = _configureProvider.GetByChannelId(ChannelId);

            if (device is not null)
            {
                _exposureMs = device.ExposureTime;
                _scansNum = device.ScansNum;
                _equalizer = device.Equalizer;
            }
        }

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

                    _frameAverager?.Reset();
                    _pcaHandler = null;

                    WeakReferenceMessenger.Default.UnregisterAll(this);

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
            Dispose(true);
        }

        #endregion
    }
}
