using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services;
using OpticEMS.Contracts.Services.Etching;
using OpticEMS.Contracts.Services.Import;
using OpticEMS.Contracts.Services.Recipe;
using OpticEMS.Notifications.Messages;
using Serilog;

namespace OpticEMS.Orchestrator
{
    public class ReprocessOrchestrator : IDisposable
    {
        private const int PlaybackIntervalMs = 33;

        private readonly IEtchingProcessService _endpointService;
        private readonly int _channelId;

        private CancellationTokenSource _cancellationToken = new();
        private bool _isRunning;
        private bool _disposed;

        public bool IsRunning => _isRunning;
        public string Status { get; private set; } = "Ready";

        public event Action<ReprocessResult>? Completed;

        public ReprocessOrchestrator(int channelId, IEtchingProcessService endpointService)
        {
            _channelId = channelId;
            _endpointService = endpointService;
        }
        
        public void Start(ImportData trace, Recipe recipe)
        {
            try
            {
                if (_isRunning)
                {
                    throw new InvalidOperationException("Reprocess is already running.");
                }

                if (trace.Series.Count == 0)
                {
                    throw new InvalidOperationException("Trace has no series data.");
                }

                _cancellationToken.Cancel();
                _cancellationToken = new CancellationTokenSource();
                _isRunning = true;
                Status = "Reprocessing...";

                Log.Information("[REPROCESS_ORCHESTRATOR]: Starting. Recipe={Recipe}, Points={Points}",
                    recipe.Name, trace.Series[0].Points.Count);

                WeakReferenceMessenger.Default.Send(new SetUpProcessChartMessage(
                    _channelId, recipe.Wavelengths, recipe.WavelengthColors));

                _endpointService.ClearConfirmedWindowsIn();
                _endpointService.ClearConfirmedWindowsOut();

                double[] initialSignal = BuildSignal(trace, recipe, 0);
                _endpointService.Start(recipe, initialSignal);

                _ = Task.Run(() => RunPlaybackAsync(trace, recipe, _cancellationToken.Token));
            }
            catch (Exception exception)
            {
                Stop();
                Log.Error(exception, "[REPROCESS_ORCHESTRATOR]: Error during reprocess start.");
            }
        }

        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _cancellationToken.Cancel();
            _endpointService.Stop();
            Status = "Stopped";

            Log.Information("[REPROCESS_ORCHESTRATOR]: Reprocess stopped.");
        }

        private async Task RunPlaybackAsync(ImportData trace, Recipe recipe, CancellationToken ct)
        {
            var timestamps = trace.Series[0].Points
                .Select(p => p.TimeSeconds)
                .ToArray();

            long nextTick = Environment.TickCount64;

            for (int frameIndex = 0; frameIndex < timestamps.Length; frameIndex++)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

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
                        break;
                    }
                }

                nextTick += PlaybackIntervalMs;

                double currentTimeSec = timestamps[frameIndex];
                double[] signal = BuildSignal(trace, recipe, frameIndex);

                var result = _endpointService.Update(signal, currentTimeSec * 1000.0);

                WeakReferenceMessenger.Default.Send(new ProcessStepUpdateMessage(
                    _channelId, result.Status, currentTimeSec, signal));

                BroadcastWindowBounds();

                Status = result.Status;

                if (result.IsDetected)
                {
                    await FinishAsync(result, currentTimeSec, forced: result.IsForced);
                    return;
                }

                now = Environment.TickCount64;
                long behind = now - nextTick;
                if (behind > PlaybackIntervalMs)
                {
                    nextTick += (behind / PlaybackIntervalMs) * PlaybackIntervalMs;
                }
            }

            if (!ct.IsCancellationRequested)
            {
                await FinishAsync(endpointResult: null, totalSeconds: trace.DurationSeconds, forced: false);
            }
        }

        private async Task FinishAsync(EndpointResult? endpointResult, double totalSeconds, bool forced)
        {
            _isRunning = false;
            _endpointService.Stop();

            string report;
            double? endpointSec = null;

            if (endpointResult?.IsDetected == true)
            {
                endpointSec = _endpointService.DetectedAtSeconds;
                double overEtch = _endpointService.OverEtchDurationSeconds;

                report = forced
                    ? $"[REPRC] Max time reached at {totalSeconds:F2}s — endpoint not found."
                    : $"[REPRC] Endpoint found at {endpointSec:F2}s\n" +
                      $"Over-etch: {overEtch:F2}s\n" +
                      $"Total: {totalSeconds:F2}s";

                Status = "Endpoint detected";
            }
            else
            {
                report = $"[REPRC] Reprocess complete — endpoint NOT detected in {totalSeconds:F2}s of data.";
                Status = "No endpoint detected";
            }

            Log.Information("[REPROCESS_ORCHESTRATOR]: {Report}", report);

            var reprocessResult = new ReprocessResult(
                EndpointFound: endpointSec.HasValue,
                EndpointSeconds: endpointSec ?? 0,
                TotalSeconds: totalSeconds,
                Report: report);

            Completed?.Invoke(reprocessResult);

            WeakReferenceMessenger.Default.Send(
                new ProcessFinishedMessage(_channelId, report, forced));
        }

        private static double[] BuildSignal(ImportData trace, Recipe recipe, int frameIndex)
        {
            var signal = new double[recipe.Wavelengths.Count];

            for (int i = 0; i < recipe.Wavelengths.Count; i++)
            {
                if (i < trace.Series.Count && frameIndex < trace.Series[i].Points.Count)
                {
                    signal[i] = trace.Series[i].Points[frameIndex].Intensity;
                }
            }

            return signal;
        }

        private void BroadcastWindowBounds()
        {
            var windowBounds = _endpointService.GetCurrentWindowBounds();
            var confirmedIn = _endpointService.GetConfirmedWindowsIn();
            var confirmedOut = _endpointService.GetConfirmedWindowsOut();

            WeakReferenceMessenger.Default.Send(new DrawWindowBoundsMessage(
                _channelId, windowBounds, confirmedIn, confirmedOut));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Stop();
            _cancellationToken.Dispose();
        }
    }
}
