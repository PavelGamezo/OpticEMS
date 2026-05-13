using OpticEMS.Contracts.Services.Etching;
using OpticEMS.Contracts.Services.Recipe;
using Serilog;

namespace OpticEMS.Services.Etching
{
    public class EtchingProcessService : IEtchingProcessService
    {
        private Recipe? _recipe;

        private readonly List<WindowBounds> _confirmedWindowsIn = new();
        private readonly List<WindowBounds> _confirmedWindowsOut = new();

        private double[] _windowStartTimes = Array.Empty<double>();
        private double[] _referenceValues = Array.Empty<double>(); 
        private double[] _fixedThresholds = Array.Empty<double>();

        private ProcessState _state = ProcessState.Idle;
        private int _consecutiveWindowsIn = 0;
        private int _consecutiveWindowsOut = 0;

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

            switch (_state)
            {
                case ProcessState.InitialDeadTime:
                    return ProcessInitialDeadTime(signal, elapsedMs);

                case ProcessState.WindowOut:
                    return ProcessWindowOutState(signal, elapsedMs);

                case ProcessState.WindowIn:
                    return ProcessWindowInState(signal, elapsedMs);

                case ProcessState.Overetch:
                    return ProcessOveretchState(elapsedMs);
            }

            return new EndpointResult(false, "Idle", false);
        }

        private EndpointResult ProcessInitialDeadTime(double[] signal, double elapsedMs)
        {
            if (elapsedMs >= _recipe.InitialDelay)
            {
                InitializeWindows(signal, elapsedMs);
                _state = ProcessState.WindowOut;
                Log.Information("[PROCESS]:InitialDeadTime -> WindowOut at {Elapsed}ms", elapsedMs);
            }

            return new EndpointResult(false, "Initial Dead Time", false);
        }

        private EndpointResult ProcessWindowOutState(double[] signal, double elapsedMs)
        {
            bool hasJustViolated = IsOutsideDetectionWindow(signal, elapsedMs);

            if (hasJustViolated)
            {
                _consecutiveWindowsOut++;
                RecordConfirmedWindowOut(elapsedMs);

                if (_consecutiveWindowsOut >= _recipe.WindowOutCount)
                {
                    Log.Information("[PROCESS]: \"WindowOut\" -> \"WindowIn\" at {ElapsedMs}", elapsedMs);
                    _state = ProcessState.WindowIn;
                    _consecutiveWindowsIn = 0;
                    _consecutiveWindowsOut = 0;
                    return new EndpointResult(false, "Monitoring", false);
                }

                return new EndpointResult(false, "Monitoring", false);
            }
            else
            {
                for (int i = 0; i < signal.Length; i++)
                {
                    if (elapsedMs - _windowStartTimes[i] >= _recipe.DetectionWindowTime)
                    {
                        _consecutiveWindowsOut = 0;
                        ClearConfirmedWindowsOut();

                        _windowStartTimes[i] = elapsedMs;
                        _referenceValues[i] = signal[i];
                    }
                }

                return new EndpointResult(false, "Monitoring", false);
            }
        }

        private EndpointResult ProcessWindowInState(double[] signal, double elapsedMs)
        {
            if (IsInsideDetectionLimits(signal))
            {
                if (CheckIfWindowShouldBeRecorded(elapsedMs))
                {
                    RecordConfirmedWindowIn(elapsedMs);
                }

                if (CheckAndSlideWindows(signal, elapsedMs))
                {
                    _consecutiveWindowsIn++;
                }

                if (_consecutiveWindowsIn >= _recipe.WindowInCount)
                {
                    _detectedAtMs = elapsedMs;
                    if (_recipe.OverEtchEnabled && _recipe.OverEtchValue > 0)
                    {
                        _state = ProcessState.Overetch;
                        _overEtchStartTime = elapsedMs;
                        Log.Information("[PROCESS]: WindowIn -> Overetching at {ElapsedMs}", elapsedMs);
                        return new EndpointResult(false, "Endpoint Found. Starting Overetch...", false);
                    }
                    else
                    {
                        _finishedAtMs = elapsedMs;
                        _state = ProcessState.Idle;
                        Log.Information("[PROCESS]: WindowIn -> Completed without overetching at {ElapsedMs}", elapsedMs);
                        return new EndpointResult(true, "Endpoint Detected", false);
                    }
                }
            }
            else
            {
                _consecutiveWindowsIn = 0;
                ClearConfirmedWindowsIn();
                ResetWindows(signal, elapsedMs);
            }

            return new EndpointResult(false, $"Monitoring", false);
        }

        private EndpointResult ProcessOveretchState(double elapsedMs)
        {
            double currentOE = elapsedMs - _overEtchStartTime;

            if (currentOE >= _recipe.OverEtchValue)
            {
                _finishedAtMs = _overEtchStartTime + _recipe.OverEtchValue;
                _state = ProcessState.Idle;
                Log.Information("[PROCESS]: Overetching -> Completed overetching at {ElapsedMs}", elapsedMs);

                return new EndpointResult(true, "Process Completed", false);
            }

            return new EndpointResult(false, $"Overetching", false);
        }

        private bool IsInsideDetectionLimits(double[] signal)
        {
            for (int i = 0; i < signal.Length; i++)
            {
                if (_referenceValues[i] <= 0)
                {
                    continue;
                }

                double threshold = _fixedThresholds[i];

                if (Math.Abs(signal[i] - _referenceValues[i]) > threshold)
                {
                    return false;
                }
            }

            return true;
        }

        private bool CheckIfWindowShouldBeRecorded(double elapsedMs)
        {
            for (int i = 0; i < _windowStartTimes.Length; i++)
            {
                if (_referenceValues[i] <= 0)
                {
                    continue;
                }

                if (elapsedMs - _windowStartTimes[i] >= _recipe.DetectionWindowTime)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CheckAndSlideWindows(double[] signal, double elapsedMs)
        {
            if (signal.Length == 0) return false;

            int movedCount = 0;

            for (int i = 0; i < signal.Length; i++)
            {
                if (_referenceValues[i] <= 0)
                {
                    continue;
                }

                if (elapsedMs - _windowStartTimes[i] >= _recipe.DetectionWindowTime)
                {
                    _windowStartTimes[i] = elapsedMs;
                    _referenceValues[i] = signal[i];

                    movedCount++;
                }
            }

            bool allMoved = movedCount == signal.Length;

            return allMoved;
        }

        private void ResetWindows(double[] signal, double elapsedMs)
        {
            for (int i = 0; i < signal.Length; i++)
            {
                _windowStartTimes[i] = elapsedMs;
                _referenceValues[i] = signal[i];
            }
        }

        private bool IsOutsideDetectionWindow(double[] currentSignal, double elapsedMs)
        {
            bool anyLineViolatedThisCycle = false;

            for (int i = 0; i < currentSignal.Length; i++)
            {
                if (_referenceValues[i] <= 0)
                {
                    continue;
                }

                double threshold = _fixedThresholds[i];
                double delta = currentSignal[i] - _referenceValues[i];

                bool violated = _recipe!.DetectionWindowHighs[i] >= 0
                    ? delta >= threshold
                    : delta <= -threshold;

                if (violated)
                {
                    anyLineViolatedThisCycle = true;

                    _windowStartTimes[i] = elapsedMs;
                    _referenceValues[i] = currentSignal[i];
                }
                else if(elapsedMs - _windowStartTimes[i] >= _recipe.DetectionWindowTime)
                {
                    _windowStartTimes[i] = elapsedMs;
                    _referenceValues[i] = currentSignal[i];
                }
            }

            return anyLineViolatedThisCycle;
        }

        private void InitializeWindows(double[] signal, double elapsedMs)
        {
            int count = signal.Length;

            if (_windowStartTimes.Length != count)
            {
                _windowStartTimes = new double[count];
                _referenceValues = new double[count];
                _fixedThresholds = new double[count];
            }

            for (int i = 0; i < count; i++)
            {
                _windowStartTimes[i] = elapsedMs;
                _referenceValues[i] = signal[i];

                double percent = Math.Abs(_recipe!.DetectionWindowHighs[i]);
                _fixedThresholds[i] = signal[i] * percent / 100.0;
            }
        }

        public void Start(Recipe recipe, double[] startIntensities)
        {
            _recipe = recipe;

            _state = ProcessState.InitialDeadTime;

            _consecutiveWindowsIn = 0;
            _consecutiveWindowsOut = 0;

            ClearConfirmedWindowsIn();
            ClearConfirmedWindowsOut();
            InitializeWindows(startIntensities, 0);

            _detectedAtMs = 0;
            _finishedAtMs = 0;
            _overEtchStartTime = 0;

            Serilog.Log.Information("[PROCESS]: Process started");
        }

        public void Stop()
        {
            _state = ProcessState.Idle;
            Serilog.Log.Information("[PROCESS]: Process stopped by system or user");
        }

        private void RecordConfirmedWindowIn(double elapsedMs)
        {
            double timeSec = elapsedMs / 1000.0;

            for (int i = 0; i < _referenceValues.Length; i++)
            {
                double half = _fixedThresholds[i];

                _confirmedWindowsIn.Add(new WindowBounds
                {
                    WavelengthIndex = i,
                    StartTime = _windowStartTimes[i] / 1000.0,
                    EndTime = (_windowStartTimes[i] + _recipe.DetectionWindowTime) / 1000.0,
                    Top = _referenceValues[i] + half,
                    Bottom = _referenceValues[i] - half,
                    Reference = _referenceValues[i]
                });
            }
        }

        private void RecordConfirmedWindowOut(double elapsedMs)
        {
            double timeSec = elapsedMs / 1000.0;

            for (int i = 0; i < _referenceValues.Length; i++)
            {
                double half = _fixedThresholds[i];

                _confirmedWindowsOut.Add(new WindowBounds
                {
                    WavelengthIndex = i,
                    StartTime = _windowStartTimes[i] / 1000.0,
                    EndTime = (_windowStartTimes[i] + _recipe.DetectionWindowTime) / 1000.0,
                    Top = _referenceValues[i] + half,
                    Bottom = _referenceValues[i] - half,
                    Reference = _referenceValues[i]
                });
            }
        }

        public List<WindowBounds> GetCurrentWindowBounds()
        {
            var bounds = new List<WindowBounds>();
            if (_recipe == null || _referenceValues.Length == 0)
                return bounds;

            for (int i = 0; i < _referenceValues.Length; i++)
            {
                double half = _fixedThresholds[i];

                bounds.Add(new WindowBounds
                {
                    WavelengthIndex = i,
                    StartTime = _windowStartTimes[i] / 1000.0,
                    EndTime = (_windowStartTimes[i] + _recipe.DetectionWindowTime) / 1000.0,
                    Top = _referenceValues[i] + half,
                    Bottom = _referenceValues[i] - half,
                    Reference = _referenceValues[i]
                });
            }
            return bounds;
        }
    }
}
