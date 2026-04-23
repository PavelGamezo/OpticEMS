using OpticEMS.Contracts.Services.Etching;
using OpticEMS.Contracts.Services.Recipe;
using System.Diagnostics;

namespace OpticEMS.Services.Etching
{
    public class EtchingProcessService : IEtchingProcessService
    {
        private Recipe? _recipe;
        private readonly Stopwatch _processTimer = new();

        private double[] _baselineSums = Array.Empty<double>();
        private double[] _finalBaselines = Array.Empty<double>();
        private int _baselineSamplesCount;

        private bool _inStableWindow;
        private bool _isOverEtching;

        private int _inConfirmCount;
        private int _outConfirmCount;

        private double[] _prevIntensities = Array.Empty<double>();
        private bool _hasPrevAvg;

        private readonly List<uint[]> _buffer = new();
        private uint[] _lastAveraged = Array.Empty<uint>();

        private double _overEtchStartTime;
        private double _detectedAtMs;
        private double _finishedAtMs;

        private string _currentStatus = "Ready";

        public double DetectedAtSeconds => _detectedAtMs / 1000.0;
        public double TotalDurationSeconds => _finishedAtMs / 1000.0;
        public double OverEtchDurationSeconds => (_finishedAtMs - _detectedAtMs) / 1000.0;

        public void PushIntensities(uint[] currentIntensities)
        {
            _buffer.Add((uint[])currentIntensities.Clone());
        }

        public EndpointResult Update()
        {
            if (_recipe == null)
            {
                return new EndpointResult(false, "No Recipe", false);
            }

            if (!_processTimer.IsRunning) 
            {
                return new EndpointResult(false, "Paused", false);
            }

            double elapsedMs = _processTimer.Elapsed.TotalMilliseconds;

            if (elapsedMs >= _recipe.MaxEndpointTime)
            {
                _finishedAtMs = elapsedMs;
                return new EndpointResult(true, "Force endpoint detected (Timeout)", true);
            }

            if (_isOverEtching)
            {
                double overEtchTime = elapsedMs - _overEtchStartTime;

                if (overEtchTime >= _recipe.OverEtchValue)
                {
                    _finishedAtMs = _overEtchStartTime + _recipe.OverEtchValue;
                    return new EndpointResult(true, "Process Finished", false);
                }

                double remaining = _recipe.OverEtchValue - overEtchTime;
                _currentStatus = $"Over-etching: {remaining / 1000.0:F1}s";
                return new EndpointResult(false, _currentStatus, false);
            }

            var currentIntensities = GetAveragedFrame();

            if (elapsedMs <= _recipe.InitialDelay)
            {
                for (int i = 0; i < currentIntensities.Length; i++)
                    _baselineSums[i] += currentIntensities[i];

                _baselineSamplesCount++;

                _currentStatus = "On going initial delay";
                return new EndpointResult(false, _currentStatus, false);
            }

            // Finalize baseline
            if (_baselineSamplesCount > 0)
            {
                for (int i = 0; i < _baselineSums.Length; i++)
                {
                    double avg = _baselineSums[i] / _baselineSamplesCount;
                    _finalBaselines[i] = avg == 0 ? currentIntensities[i] : avg;
                }
                _baselineSamplesCount = 0;
            }

            // Window-IN (stabilization)
            if (!_inStableWindow)
            {
                if (!_hasPrevAvg)
                {
                    _prevIntensities = currentIntensities.Select(v => (double)v).ToArray();
                    _hasPrevAvg = true;
                    return new EndpointResult(false, "Stabilizing...", false);
                }

                double sumDelta = 0;
                int count = currentIntensities.Length;

                for (int i = 0; i < count; i++)
                {
                    double prev = _prevIntensities[i];
                    double curr = currentIntensities[i];

                    if (prev == 0)
                        continue;

                    double delta = Math.Abs(curr - prev) / prev * 100.0;
                    sumDelta += delta;
                }

                double avgDelta = sumDelta / count;

                if (avgDelta < _recipe.StableThresholdPercent)
                {
                    _inConfirmCount++;
                }
                else
                {
                    _inConfirmCount = 0;
                    _prevIntensities = currentIntensities.Select(v => (double)v).ToArray();
                }

                if (_inConfirmCount >= _recipe.WindowInCount)
                    _inStableWindow = true;

                return new EndpointResult(false, "Stabilizing...", false);
            }

            // Window-OUT (endpoint detection)
            bool changed = CheckIfSignalChanged(currentIntensities);

            _outConfirmCount = changed ? _outConfirmCount + 1 : 0;

            if (_outConfirmCount >= _recipe.WindowOutCount)
            {
                _detectedAtMs = elapsedMs;

                if (_recipe.OverEtchEnabled && _recipe.OverEtchValue > 0)
                {
                    _isOverEtching = true;
                    _overEtchStartTime = elapsedMs;
                    return new EndpointResult(false, "Over-etching...", false);
                }

                _finishedAtMs = elapsedMs;
                return new EndpointResult(true, "Endpoint Detected", false);
            }

            _currentStatus = "Monitoring...";
            return new EndpointResult(false, _currentStatus, false);
        }

        private uint[] GetAveragedFrame()
        {
            if (_buffer.Count == 0)
            {
                return _lastAveraged.Length > 0 ? _lastAveraged : new uint[_finalBaselines.Length];
            }

            int length = _buffer[0].Length;
            double[] sum = new double[length];

            foreach (var frame in _buffer)
            {
                for (int i = 0; i < length; i++)
                {
                    sum[i] += frame[i];
                }
            }

            uint[] avg = new uint[length];
            for (int i = 0; i < length; i++)
            {
                avg[i] = (uint)(sum[i] / _buffer.Count);
            }

            _buffer.Clear();
            _lastAveraged = avg;

            return avg;
        }

        private bool CheckIfSignalChanged(uint[] currentIntensities)
        {
            for (int i = 0; i < _finalBaselines.Length; i++)
            {
                double baseline = _finalBaselines[i];
                if (baseline <= 0)
                {
                    baseline = currentIntensities[i];
                }

                double deltaPercent = Math.Abs(currentIntensities[i] - baseline) / baseline * 100.0;

                if (deltaPercent >= _recipe.DetectionWindowHighs[i])
                    return true;
            }

            return false;
        }

        public void Start(Recipe recipe, uint[] startIntensities)
        {
            _recipe = recipe;
            _processTimer.Restart();

            int count = startIntensities.Length;

            _baselineSums = new double[count];
            _finalBaselines = new double[count];
            _baselineSamplesCount = 0;

            _inStableWindow = false;
            _isOverEtching = false;

            _inConfirmCount = 0;
            _outConfirmCount = 0;

            _hasPrevAvg = false; 
            _prevIntensities = new double[count];

            _detectedAtMs = 0;
            _finishedAtMs = 0;

            _buffer.Clear();
            _lastAveraged = Array.Empty<uint>();

            _currentStatus = "On going initial delay";
        }

        public void Pause() => _processTimer.Stop();
        public void Resume() => _processTimer.Start();

        public void Stop()
        {
            _processTimer.Stop();
            _recipe = null;
            _inStableWindow = false;
            _currentStatus = "Stopped";
        }
    }
}
