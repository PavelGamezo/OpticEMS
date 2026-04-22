using OpticEMS.MVVM.Models;
using OpticEMS.MVVM.Models.Recipe;
using System.Diagnostics;

namespace OpticEMS.Services.Etching
{
    public class EtchingProcessService : IEtchingProcessService
    {
        private RecipeModel? _recipe; 
        private readonly Stopwatch _processTimer = new();

        private double[] _baselineSums = Array.Empty<double>();
        private int _baselineSamplesCount;
        private double[] _finalBaselines = Array.Empty<double>();

        private int _inConfirmCount;
        private int _outConfirmCount;
        private bool _inStableWindow;
        private bool _isOverEtching;
        private double _overEtchStartTime;
        private double _detectedAtMs;
        private double _finishedAtMs;

        private string _currentStatus = "Ready";

        public double DetectedAtSeconds => _detectedAtMs / 1000.0;

        public double TotalDurationSeconds => _finishedAtMs / 1000.0;

        public double OverEtchDurationSeconds => (_finishedAtMs - _detectedAtMs) / 1000.0;

        public EndpointResult Update(uint[] currentIntensities)
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
                double timeInOverEtch = elapsedMs - _overEtchStartTime;

                if (timeInOverEtch >= _recipe.OverEtchValue)
                {
                    _finishedAtMs = _overEtchStartTime + _recipe.OverEtchValue;

                    return new EndpointResult(true, "Process Finished", false);
                }

                double remaining = _recipe.OverEtchValue - timeInOverEtch;

                _currentStatus = $"Over-etching: {remaining / 1000.0:F1}s";
                return new EndpointResult(false, _currentStatus, false);
            }

            if (elapsedMs <= _recipe.InitialDelay)
            {
                for (int i = 0; i < currentIntensities.Length; i++)
                {
                    if (i < _baselineSums.Length) _baselineSums[i] += currentIntensities[i];
                }
                _baselineSamplesCount++;

                _currentStatus = "On going initial delay";
                return new EndpointResult(false, _currentStatus, false);
            }

            if (_baselineSamplesCount > 0)
            {
                for (int i = 0; i < _baselineSums.Length; i++)
                {
                    _finalBaselines[i] = _baselineSums[i] / _baselineSamplesCount;
                }
                _baselineSamplesCount = 0;
            }

            bool isSignalChanged = CheckIfSignalChanged(currentIntensities);

            if (!_inStableWindow)
            {
                _inConfirmCount = !isSignalChanged ? _inConfirmCount + 1 : 0;

                if (_inConfirmCount >= _recipe.WindowInCount)
                {
                    _inStableWindow = true;
                }
                _currentStatus = "Monitoring...";
            }
            else
            {
                _outConfirmCount = isSignalChanged ? _outConfirmCount + 1 : 0;

                if (_outConfirmCount >= _recipe.WindowOutCount)
                {
                    _detectedAtMs = elapsedMs;

                    if (_recipe.OverEtchEnabled && _recipe.OverEtchValue > 0)
                    {
                        _isOverEtching = true;
                        _overEtchStartTime = elapsedMs;
                        _currentStatus = "Over-etching...";
                        return new EndpointResult(false, _currentStatus, false);
                    }

                    _finishedAtMs = elapsedMs;
                    return new EndpointResult(true, "Endpoint Detected", false);
                }
                _currentStatus = "Monitoring...";
            }

            return new EndpointResult(false, _currentStatus, false);
        }

        private bool CheckIfSignalChanged(uint[] currentIntensities)
        {
            bool anyChange = false;

            for (int i = 0; i < _finalBaselines.Length; i++)
            {
                if (i >= currentIntensities.Length)
                {
                    break;
                }

                if (_recipe.DetectionWindowHighs[i] == 0)
                {
                    continue;
                }

                double baseline = _finalBaselines[i];
                if (baseline == 0)
                {
                    continue;
                }

                double deltaPercent = Math.Abs(currentIntensities[i] - baseline) / baseline * 100.0;
                if (deltaPercent >= _recipe.DetectionWindowHighs[i])
                {
                    anyChange = true;
                }
                else
                {
                    return false;
                }
            }

            return anyChange;
        }

        public void Start(RecipeModel recipe, uint[] startIntensities)
        {
            _recipe = recipe;
            _processTimer.Restart();

            _inConfirmCount = 0;
            _outConfirmCount = 0;
            _inStableWindow = false;
            _isOverEtching = false;
            _overEtchStartTime = 0;
            _detectedAtMs = 0;
            _finishedAtMs = 0;

            int count = startIntensities.Length;
            _baselineSums = new double[count];
            _baselineSamplesCount = 0;
            _finalBaselines = new double[count];

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
