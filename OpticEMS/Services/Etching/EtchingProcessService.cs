using OpticEMS.MVVM.Models;

namespace OpticEMS.Services.Etching
{
    public class EtchingProcessService : IEtchingProcessService
    {
        private RecipeModel? _recipe;
        private List<double> _baselines = new();
        private int _inConfirmCount;
        private int _outConfirmCount;
        private bool _inStableWindow;

        public EndpointResult CheckEndpoint(uint[] currentIntensities, double elapsedMs)
        {
            if (elapsedMs >= _recipe.MaxEndpointTime)
            {
                return new EndpointResult(true, "Force endpoint detected", true);
            }

            if (elapsedMs <= _recipe.InitialDelay && currentIntensities.Length > 0)
            {
                _baselines.Clear();

                foreach (var intensity in currentIntensities)
                {
                    _baselines.Add(intensity);
                }

                return new EndpointResult(false, "On going initial delay", false);
            }

            bool isSignalChanged = true;

            for (int i = 0; i < _baselines.Count; i++)
            {
                var delta = (currentIntensities[i] - _baselines[i]) / _baselines[i] * 100;

                if (_recipe.DetectionWindowHighs[i] != 0)
                {
                    bool isValueChanged = Math.Abs(delta) >= _recipe.DetectionWindowHighs[i];

                    isSignalChanged = isSignalChanged && isValueChanged;
                }
            }

            if (!_inStableWindow)
            {
                if (!isSignalChanged)
                {
                    _inConfirmCount++;
                }
                else
                {
                    _inConfirmCount = 0;
                }

                if (_inConfirmCount >= _recipe.WindowInCount)
                {
                    _inStableWindow = true;

                    return new(false, "Monitoring Endpoint...", false);
                }
            }
            else
            {
                if (isSignalChanged)
                {
                    _outConfirmCount++;
                }
                else
                {
                    _outConfirmCount = 0;
                }

                if (_outConfirmCount >= _recipe.WindowOutCount)
                {
                    return new(true, "Endpoint detected", false);
                }
            }

            return new EndpointResult(false, _inStableWindow ? "Monitoring Endpoint..." : "Stabilizing...", false);
        }

        public void Start(RecipeModel recipe, uint[] startIntensities)
        {
            _recipe = recipe;
            _inConfirmCount = 0;
            _outConfirmCount = 0;
            _inStableWindow = false;
            _baselines.Clear();

            foreach (var startIntensity in startIntensities) 
            {
                _baselines.Add(startIntensity);
            }
        }

        public void Stop()
        {
            _recipe = null;
            _inStableWindow = false;
            _baselines.Clear();
        }
    }
}
