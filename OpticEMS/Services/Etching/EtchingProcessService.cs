using OpticEMS.Contracts.Services.Etching;
using OpticEMS.Contracts.Services.Recipe;
using Serilog;

namespace OpticEMS.Services.Etching
{
    public class EtchingProcessService : IEtchingProcessService
    {
        private Recipe? _recipe;
        private List<State> WavelengthStates = new();

        private readonly List<WindowBounds> _confirmedWindowsIn = new();
        private readonly List<WindowBounds> _confirmedWindowsOut = new();

        private ProcessState _state = ProcessState.Idle;

        private double _detectedAtMs;
        private double _finishedAtMs;
        private double _overEtchStartTime;

        public double DetectedAtSeconds => _detectedAtMs / 1000.0;
        public double TotalDurationSeconds => _finishedAtMs / 1000.0;
        public double OverEtchDurationSeconds => (_finishedAtMs - _detectedAtMs) / 1000.0;

        public List<WindowBounds> GetConfirmedWindowsIn()
        {
            lock (_confirmedWindowsIn)
            {
                return _confirmedWindowsIn.ToList();
            }
        }

        public void ClearConfirmedWindowsIn()
        {
            lock (_confirmedWindowsIn)
            {
                _confirmedWindowsIn.Clear();
            }
        }

        public List<WindowBounds> GetConfirmedWindowsOut()
        {
            lock (_confirmedWindowsOut)
            {
                return _confirmedWindowsOut.ToList();
            }
        }

        public void ClearConfirmedWindowsOut()
        {
            lock (_confirmedWindowsOut)
            {
                _confirmedWindowsOut.Clear();
            }
        }

        public EndpointResult Update(double[] signal, double elapsedMs)
        {
            if (_recipe == null)
            {
                return new EndpointResult(false, "No Recipe", false);
            }

            if (signal == null || signal.Length == 0)
            {
                return new EndpointResult(false, "Waiting for signal...", false);
            }

            if (elapsedMs >= _recipe.MaxEndpointTime)
            {
                _finishedAtMs = elapsedMs;
                _state = ProcessState.Idle;

                return new EndpointResult(true, "Force Stop (Timeout)", true);
            }

            if (_state == ProcessState.InitialDeadTime)
            {
                if (elapsedMs >= _recipe.InitialDelay)
                {
                    InitializeWindows(signal, elapsedMs);
                    _state = ProcessState.WindowOut;
                    Log.Information("[PROCESS]: InitialDeadTime -> WindowOut at {Elapsed}ms", elapsedMs);
                }

                return new EndpointResult(false, "Initial Dead Time", false);
            }

            if (_state == ProcessState.WindowOut && AllChannelsReachedWindowIn())
            {
                foreach (var state in WavelengthStates)
                {
                    if (!state.WindowInDisabled)
                    {
                        state.HasReachedWindowIn = false;
                    }

                    state.HasReachedWindowIn = false;
                }

                _detectedAtMs = elapsedMs;
                Log.Information("[PROCESS] All channels reached WindowIn → Endpoint at {0:F2}s", elapsedMs / 1000.0);

                if (_recipe.OverEtchEnabled && _recipe.OverEtchValue > 0)
                {
                    _overEtchStartTime = elapsedMs;
                    _state = ProcessState.Overetch;
                    return new EndpointResult(false, "Overetching", false);
                }
                else
                {
                    _finishedAtMs = elapsedMs;
                    _state = ProcessState.Idle;
                    return new EndpointResult(true, "Endpoint Detected", false);
                }
            }

            foreach (var state in WavelengthStates)
            {
                ProcessSingleWavelength(state, signal[state.Index], elapsedMs);
            }

            if (_state == ProcessState.Overetch)
            {
                return ProcessOveretchState(elapsedMs);
            }

            return new EndpointResult(false, "Monitoring", false);
        }

        private void ProcessSingleWavelength(State state, double signal, double elapsedMs)
        {
            switch (state.ProcessState)
            {
                case ProcessState.WindowOut:
                    ProcessWindowOutState(state, signal, elapsedMs);
                    break;

                case ProcessState.WindowIn:
                    ProcessWindowInState(state, signal, elapsedMs);
                    break;
            }
        }

        private void ProcessWindowOutState(State state, double signal, double elapsedMs)
        {
            if (state.WindowOutDisabled)
            {
                return;
            }

            bool hasJustViolated = IsOutsideDetectionWindow(state, signal, elapsedMs);

            if (hasJustViolated)
            {
                RecordConfirmedWindowOut(state, elapsedMs);
                state.ConsecutiveOut++;

                if (state.ConsecutiveOut >= state.WindowOutCount)
                {
                    Log.Information("[PROCESS]: Channel {Index} WindowOut → WindowIn at {Elapsed}ms",
                        state.Index, elapsedMs);

                    if (state.WindowInDisabled)
                    {
                        state.HasReachedWindowIn = true;
                    }
                    else
                    {
                        state.ProcessState = ProcessState.WindowIn;
                    }

                    state.ConsecutiveIn = 0;
                    state.ConsecutiveOut = 0;
                }

                state.WindowStartTime = elapsedMs;
                state.Reference = signal;
            }
            else
            {
                if (elapsedMs - state.WindowStartTime >= state.DetectionWindowTime)
                {
                    if (state.ConsecutiveOut > 0)
                    {
                        state.ConsecutiveOut = 0;
                        ClearConfirmedWindowsOut();
                    }

                    state.WindowStartTime = elapsedMs;
                    state.Reference = signal;
                }
            }
        }

        private void ProcessWindowInState(State state, double signal, double elapsedMs)
        {
            if (state.WindowInDisabled)
            {
                return;
            }

            if (IsInsideDetectionLimits(state, signal))
            {
                if (CheckIfWindowShouldBeRecorded(state, elapsedMs))
                {
                    RecordConfirmedWindowIn(state, elapsedMs);
                }

                if (CheckWindow(state, signal, elapsedMs))
                {
                    state.ConsecutiveIn++;
                }

                if (state.ConsecutiveIn >= state.WindowInCount)
                {
                    state.HasReachedWindowIn = true;
                    state.ProcessState = ProcessState.Overetch;
                }

                if (!state.HasReachedWindowIn)
                {
                    CheckAndSlideWindows(state, signal, elapsedMs);
                }
            }
            else
            {
                state.ConsecutiveIn = 0;
                ClearConfirmedWindowsIn();
                ResetWindows(state, signal, elapsedMs);
            }
        }

        private EndpointResult ProcessOveretchState(double elapsedMs)
        {
            double currentOE = elapsedMs - _overEtchStartTime;

            if (currentOE >= _recipe!.OverEtchValue)
            {
                _finishedAtMs = _overEtchStartTime + _recipe.OverEtchValue;
                _state = ProcessState.Idle;
                Log.Information("[PROCESS]: Overetching -> Completed overetching at {ElapsedMs}", elapsedMs);

                return new EndpointResult(true, "Process Completed", false);
            }

            return new EndpointResult(false, $"Overetching", false);
        }

        private bool AllChannelsReachedWindowIn() =>
            WavelengthStates.All(state => state.HasReachedWindowIn);

        private bool IsInsideDetectionLimits(State state, double signal)
        {
            if (state.Threshold <= 0)
            {
                return true;
            }

            double threshold = state.Threshold;

            if (Math.Abs(signal - state.Reference) > threshold)
            {
                return false;
            }

            return true;
        }

        private bool CheckIfWindowShouldBeRecorded(State state, double elapsedMs)
        {
            if (state.Threshold <= 0)
            {
                return true;
            }

            if (elapsedMs - state.WindowStartTime >= state.DetectionWindowTime)
            {
                return true;
            }

            return false;
        }

        private bool CheckWindow(State state, double signal, double elapsedMs)
        {
            return CheckIfWindowShouldBeRecorded(state, elapsedMs);
        }

        private void CheckAndSlideWindows(State state, double signal, double elapsedMs)
        {
            if (state.Threshold <= 0)
            {
                return;
            }

            if (elapsedMs - state.WindowStartTime >= state.DetectionWindowTime)
            {
                state.WindowStartTime = elapsedMs;
                state.Reference = signal;

                return;
            }
        }

        private void ResetWindows(State state, double signal, double elapsedMs)
        {
            state.WindowStartTime = elapsedMs;
            state.Reference = signal;
        }

        private bool IsOutsideDetectionWindow(State state, double currentSignal, double elapsedMs)
        {
            bool anyLineViolatedThisCycle = false;

            if (state.Threshold <= 0)
            {
                return true;
            }

            double threshold = state.Threshold;
            double delta = currentSignal - state.Reference;

            bool violated = _recipe!.DetectionWindowHighs[state.Index] >= 0
                ? delta >= threshold
                : delta <= -threshold;

            if (violated)
            {
                anyLineViolatedThisCycle = true;
            }
            else if (elapsedMs - state.WindowStartTime >= state.DetectionWindowTime)
            {
                state.WindowStartTime = elapsedMs;
                state.Reference = currentSignal;
            }

            return anyLineViolatedThisCycle;
        }

        private void InitializeWindows(double[] signal, double elapsedMs)
        {
            for (int i = 0; i < signal.Length; i++)
            {
                WavelengthStates[i].WindowStartTime = elapsedMs;
                WavelengthStates[i].Reference = signal[i];

                double threshold = Math.Abs(_recipe!.DetectionWindowHighs[i]);

                WavelengthStates[i].Threshold = threshold > 0 ? threshold : 0.001;
            }
        }

        public void Start(Recipe recipe, double[] startIntensities)
        {
            _recipe = recipe;

            _state = ProcessState.InitialDeadTime;

            WavelengthStates = new List<State>();
            for (int i = 0; i < startIntensities.Length; i++)
            {
                bool isMonitoringDisabled = _recipe.DetectionWindowHighs[i] == 0;
                bool windowOutDisabled = _recipe.WindowOutCounts[i] == 0;
                bool windowInDisabled = _recipe.WindowInCounts[i] == 0;

                ProcessState initialState;
                bool hasReachedWindowIn;

                if (isMonitoringDisabled)
                {
                    initialState = ProcessState.Overetch;
                    hasReachedWindowIn = true;
                }
                else if (windowOutDisabled && windowInDisabled)
                {
                    initialState = ProcessState.WindowOut;
                    hasReachedWindowIn = false;
                }
                else if (windowOutDisabled)
                {
                    initialState = ProcessState.WindowIn;
                    hasReachedWindowIn = false;
                }
                else
                {
                    initialState = ProcessState.WindowOut;
                    hasReachedWindowIn = false;
                }

                var state = new State
                {
                    Index = i,
                    ProcessState = initialState,
                    Reference = startIntensities[i],
                    HasReachedWindowIn = hasReachedWindowIn,

                    DetectionWindowTime = _recipe.DetectionWindowTimes[i],
                    WindowInCount = _recipe.WindowInCounts[i],
                    WindowOutCount = _recipe.WindowOutCounts[i],

                    WindowOutDisabled = windowOutDisabled,
                    WindowInDisabled = windowInDisabled,
                };

                WavelengthStates.Add(state);

                Log.Debug("[PROCESS]: Channel {Index} — WindowOut={Out}, WindowIn={In}, " +
                          "OutDisabled={OutDis}, InDisabled={InDis}, MonDisabled={MonDis}",
                    i,
                    _recipe.WindowOutCounts[i], _recipe.WindowInCounts[i],
                    windowOutDisabled, windowInDisabled, isMonitoringDisabled);
            }

            ClearConfirmedWindowsIn();
            ClearConfirmedWindowsOut();

            _detectedAtMs = 0;
            _finishedAtMs = 0;
            _overEtchStartTime = 0;

            Log.Information("[PROCESS]: Started. Recipe='{Name}', Channels={Count}, MaxTime={Max}s",
                recipe.Name, startIntensities.Length, recipe.MaxEndpointTime / 1000.0);
        }

        public void Stop()
        {
            _state = ProcessState.Idle;
            Log.Information("[PROCESS]: Process stopped by system or user");
        }

        private void RecordConfirmedWindowIn(State state, double elapsedMs)
        {
            double timeSec = elapsedMs / 1000.0;

            double half = state.Threshold;

            _confirmedWindowsIn.Add(new WindowBounds
            {
                WavelengthIndex = state.Index,
                StartTime = state.WindowStartTime / 1000.0,
                EndTime = (state.WindowStartTime + state.DetectionWindowTime) / 1000.0,
                Top = state.Reference + half,
                Bottom = state.Reference - half,
                Reference = state.Reference
            });
        }

        private void RecordConfirmedWindowOut(State state, double elapsedMs)
        {
            double timeSec = elapsedMs / 1000.0;

            double half = state.Threshold;

            _confirmedWindowsOut.Add(new WindowBounds
            {
                WavelengthIndex = state.Index,
                StartTime = state.WindowStartTime / 1000.0,
                EndTime = (state.WindowStartTime + state.DetectionWindowTime) / 1000.0,
                Top = state.Reference + half,
                Bottom = state.Reference - half,
                Reference = state.Reference
            });
        }

        public List<WindowBounds> GetCurrentWindowBounds()
        {
            var bounds = new List<WindowBounds>();

            foreach (var state in WavelengthStates)
            {
                if (_recipe == null || state.Threshold <= 0)
                {
                    return bounds;
                }

                double half = state.Threshold;

                bounds.Add(new WindowBounds
                {
                    WavelengthIndex = state.Index,
                    StartTime = state.WindowStartTime / 1000.0,
                    EndTime = (state.WindowStartTime + state.DetectionWindowTime) / 1000.0,
                    Top = state.Reference + half,
                    Bottom = state.Reference - half,
                    Reference = state.Reference
                });
            }

            return bounds;
        }
    }
}
