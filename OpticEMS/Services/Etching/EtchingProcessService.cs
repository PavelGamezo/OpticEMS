using OpticEMS.Contracts.Services.Etching;
using OpticEMS.Contracts.Services.Recipe;
using OpticEMS.Preprocessing;

namespace OpticEMS.Services.Etching
{
    public class EtchingProcessService : IEtchingProcessService
    {
        private Recipe? _recipe;
        private TrendEquationsHandler? _trendHandler;

        private double[] _windowStartTimes = Array.Empty<double>();
        private double[] _referenceValues = Array.Empty<double>();
        private double[] _baseline = Array.Empty<double>();

        private ProcessState _state = ProcessState.Idle;
        private int _consecutiveWindowsIn = 0;
        private int _consecutiveWindowsOut = 0;

        private double _detectedAtMs;
        private double _finishedAtMs;
        private double _overEtchStartTime;

        public double DetectedAtSeconds => _detectedAtMs / 1000.0;
        public double TotalDurationSeconds => _finishedAtMs / 1000.0;
        public double OverEtchDurationSeconds => (_finishedAtMs - _detectedAtMs) / 1000.0;

        public void PushIntensities(double[] currentIntensities)
        {
            _trendHandler?.PushIntensities(currentIntensities);
        }

        public EndpointResult Update(double elapsedMs)
        {
            if (_recipe == null)
            {
                return new EndpointResult(false, "No Recipe", false);
            }

            if (elapsedMs >= _recipe.MaxEndpointTime)
            {
                _finishedAtMs = elapsedMs;
                _state = ProcessState.Idle;

                return new EndpointResult(true, "Force Stop (Timeout)", true);
            }

            var currentSignal = GetProcessedSignal(elapsedMs);
            if (currentSignal == null || currentSignal.Length == 0)
            {
                return new EndpointResult(false, "Waiting for signal...", false);
            }

            switch (_state)
            {
                case ProcessState.InitialDeadTime:
                    return ProcessInitialDeadTime(currentSignal, elapsedMs);

                case ProcessState.WindowOut:
                    return ProcessWindowOutState(currentSignal, elapsedMs);

                case ProcessState.WindowIn:
                    return ProcessWindowInState(currentSignal, elapsedMs);

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
            }

            return new EndpointResult(false, "Initial Dead Time", false);
        }

        private EndpointResult ProcessWindowOutState(double[] signal, double elapsedMs)
        {
            bool violated = IsOutsideDetectionWindow(signal, elapsedMs);
            if (violated)
            {
                _consecutiveWindowsOut++;
                if (_consecutiveWindowsOut >= _recipe.WindowOutCount)
                {
                    _state = ProcessState.WindowIn;
                    _consecutiveWindowsIn = 0;

                    return new EndpointResult(false, "Monitoring", false);
                }
            }
            else
            {
                bool allWindowsExpired = true;
                for (int i = 0; i < _windowStartTimes.Length; i++)
                {
                    if (elapsedMs - _windowStartTimes[i] < _recipe.DetectionWindowTime)
                    {
                        allWindowsExpired = false;
                        break;
                    }
                }
                if (allWindowsExpired)
                    _consecutiveWindowsOut = 0;
            }

            return new EndpointResult(false, $"Monitoring", false);
        }

        private EndpointResult ProcessWindowInState(double[] signal, double elapsedMs)
        {
            if (IsInsideDetectionLimits(signal))
            {
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
                        return new EndpointResult(false, "Endpoint Found. Starting Overetch...", false);
                    }
                    else
                    {
                        _finishedAtMs = elapsedMs;
                        _state = ProcessState.Idle;
                        return new EndpointResult(true, "Endpoint Detected", false);
                    }
                }
            }
            else
            {
                _consecutiveWindowsIn = 0;
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

                return new EndpointResult(true, "Process Completed", false);
            }

            return new EndpointResult(false, $"Overetching", false);
        }

        private bool IsInsideDetectionLimits(double[] signal)
        {
            for (int i = 0; i < signal.Length; i++)
            {
                double delta = Math.Abs((double)signal[i] - _referenceValues[i]);
                double threshold = Math.Abs(_recipe.DetectionWindowHighs[i]) / 2.0;
                
                if (delta > threshold)
                {
                    return false;
                }
            }
            return true;
        }

        private double[]? GetProcessedSignal(double elapsedMs)
        {
            var trendResult = _trendHandler?.Process(elapsedMs);

            return trendResult?.Smoothed;
        }

        private bool CheckAndSlideWindows(double[] signal, double elapsedMs)
        {
            bool anyMoved = false;

            for (int i = 0; i < signal.Length; i++)
            {
                if (elapsedMs - _windowStartTimes[i] >= _recipe.DetectionWindowTime)
                {
                    _windowStartTimes[i] = elapsedMs;
                    _referenceValues[i] = signal[i];
                    anyMoved = true;
                }
            }

            return anyMoved;
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

                double delta = Math.Abs((double)currentSignal[i] - _referenceValues[i]);
                double allowedTolerance = Math.Abs(_recipe.DetectionWindowHighs[i]) / 2.0;

                if (delta >= allowedTolerance)
                {
                    anyLineViolatedThisCycle = true;

                    _windowStartTimes[i] = elapsedMs;
                    _referenceValues[i] = currentSignal[i];
                }
                else
                {
                    if (elapsedMs - _windowStartTimes[i] >= _recipe.DetectionWindowTime)
                    {
                        _windowStartTimes[i] = elapsedMs;
                        _referenceValues[i] = currentSignal[i];
                    }
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
                _baseline = new double[count];
            }

            for (int i = 0; i < count; i++)
            {
                _windowStartTimes[i] = elapsedMs;
                _referenceValues[i] = signal[i];
                _baseline[i] = signal[i];
            }
        }

        public void Start(Recipe recipe, double[] startIntensities)
        {
            _recipe = recipe;
            
            _trendHandler = new TrendEquationsHandler(recipe.DerivativeEnabled);
            _trendHandler.Set(recipe.MagneticFieldPeriodMs, recipe.FieldPeriodsToAverage);

            _state = ProcessState.InitialDeadTime;

            _consecutiveWindowsIn = 0;
            _consecutiveWindowsOut = 0;

            InitializeWindows(startIntensities, 0);

            _detectedAtMs = 0;
            _finishedAtMs = 0;
            _overEtchStartTime = 0;
        }

        public void Stop() => _state = ProcessState.Idle;


        public List<WindowBounds> GetCurrentWindowBounds()
        {
            var bounds = new List<WindowBounds>();
            if (_recipe == null) return bounds;

            for (int i = 0; i < _referenceValues.Length; i++)
            {
                double totalHeight = Math.Abs(_recipe.DetectionWindowHighs[i]);
                double refVal = _referenceValues[i];

                double half = totalHeight / 2.0;

                bounds.Add(new WindowBounds
                {
                    WavelengthIndex = i,
                    StartTime = _windowStartTimes[i] / 1000.0,
                    EndTime = (_windowStartTimes[i] + _recipe.DetectionWindowTime) / 1000.0,
                    Top = refVal + half,
                    Bottom = refVal - half,
                    Reference = refVal
                });
            }
            return bounds;
        }
    }
}
