using OpticEMS.Contracts.Services.Etching;
using OpticEMS.Contracts.Services.Recipe;

namespace OpticEMS.Services.Etching
{
    public class EtchingProcessService : IEtchingProcessService
    {
        private Recipe? _recipe;
        private readonly object _swapLock = new();

        private double[] _windowStartTimes = Array.Empty<double>();
        private double[] _referenceValues = Array.Empty<double>();
        private double[] _baseline = Array.Empty<double>();

        private List<uint[]> _writeBuffer = new();
        private List<uint[]> _readBuffer = new();
        private uint[] _lastAveraged = Array.Empty<uint>();

        private ProcessState _state = ProcessState.Idle;
        private int _consecutiveWindowsIn = 0;
        private int _consecutiveWindowsOut = 0;

        private double _detectedAtMs;
        private double _finishedAtMs;
        private double _overEtchStartTime;

        public double DetectedAtSeconds => _detectedAtMs / 1000.0;
        public double TotalDurationSeconds => _finishedAtMs / 1000.0;
        public double OverEtchDurationSeconds => (_finishedAtMs - _detectedAtMs) / 1000.0;

        public void PushIntensities(uint[] currentIntensities)
        {
            lock (_swapLock)
            {
                _writeBuffer.Add((uint[])currentIntensities.Clone());
            }
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

            var currentSignal = GetAveragedFrame();
            if (currentSignal == null || currentSignal.Length == 0)
            {
                return new EndpointResult(false, "Waiting for signal...", false);
            }

            switch (_state)
            {
                case ProcessState.InitialDeadTime:

                    if (elapsedMs >= _recipe.InitialDelay)
                    {
                        InitializeWindows(currentSignal, elapsedMs);
                        _state = ProcessState.WindowIn;
                    }

                    return new EndpointResult(false, "Initial Dead Time", false);

                case ProcessState.WindowIn:

                    if (IsInsideDetectionLimits(currentSignal))
                    {
                        if (CheckAndSlideWindows(currentSignal, elapsedMs))
                        {
                            _consecutiveWindowsIn++;
                        }

                        if (_consecutiveWindowsIn >= _recipe.WindowInCount)
                        {
                            _state = ProcessState.Monitoring;
                            _consecutiveWindowsOut = 0;
                        }
                    }
                    else
                    {
                        _consecutiveWindowsIn = 0;
                        ResetWindows(currentSignal, elapsedMs);
                    }

                    return new EndpointResult(false, $"Stabilizing ({_consecutiveWindowsIn}/{_recipe.WindowInCount})", false);

                case ProcessState.Monitoring:

                    bool violated = IsOutsideDetectionWindow(currentSignal, elapsedMs);
                    if (violated)
                    {
                        _consecutiveWindowsOut++;
                        if (_consecutiveWindowsOut >= _recipe.WindowOutCount)
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
                        {
                            _consecutiveWindowsOut = 0;
                        }
                    }

                    return new EndpointResult(false, $"Monitoring", false);

                case ProcessState.Overetch:

                    double currentOE = elapsedMs - _overEtchStartTime;

                    if (currentOE >= _recipe.OverEtchValue)
                    {
                        _finishedAtMs = _overEtchStartTime + _recipe.OverEtchValue;
                        _state = ProcessState.Idle;

                        return new EndpointResult(true, "Process Completed", false);
                    }

                    double remaining = (_recipe.OverEtchValue - currentOE) / 1000.0;

                    return new EndpointResult(false, $"Overetching", false);
            }

            return new EndpointResult(false, "Idle", false);
        }

        private bool IsInsideDetectionLimits(uint[] signal)
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

        private bool CheckAndSlideWindows(uint[] signal, double elapsedMs)
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

        private void ResetWindows(uint[] signal, double elapsedMs)
        {
            for (int i = 0; i < signal.Length; i++)
            {
                _windowStartTimes[i] = elapsedMs;
                _referenceValues[i] = signal[i];
            }
        }

        private bool IsOutsideDetectionWindow(uint[] currentSignal, double elapsedMs)
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

        private void InitializeWindows(uint[] signal, double elapsedMs)
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

        private uint[] GetAveragedFrame()
        {
            lock (_swapLock)
            {
                if (_writeBuffer.Count == 0) return _lastAveraged;

                var tmp = _readBuffer;
                _readBuffer = _writeBuffer;
                _writeBuffer = tmp;
                _writeBuffer.Clear();
            }

            int length = _readBuffer[0].Length;
            double[] sum = new double[length];

            foreach (var frame in _readBuffer)
            {
                for (int i = 0; i < length; i++) sum[i] += frame[i];
            }

            uint[] avg = new uint[length];
            for (int i = 0; i < length; i++)
            {
                avg[i] = (uint)(sum[i] / _readBuffer.Count);
            }

            _lastAveraged = avg;

            return avg;
        }

        public void Start(Recipe recipe, uint[] startIntensities)
        {
            _recipe = recipe;
            _state = ProcessState.InitialDeadTime;

            _consecutiveWindowsIn = 0;
            _consecutiveWindowsOut = 0;

            InitializeWindows(startIntensities, 0);

            _readBuffer.Clear();
            _writeBuffer.Clear();
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
