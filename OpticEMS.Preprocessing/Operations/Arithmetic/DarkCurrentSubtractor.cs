using Serilog;

namespace OpticEMS.Preprocessing
{
    public sealed class DarkCurrentSubtractor
    {
        private const int FRAMES = 5;

        private double[]? _baseline;
        private double[]? _accumulator;
        private int _framesCollected;
        private bool _isReady;

        public bool IsReady => _isReady;

        public double[]? Process(double[] intensities)
        {
            if (intensities is null)
            {
                Log.Warning("[DARK_CURRENT_SERVICE]: Intensities array is null.");
                return null;
            }

            if (!_isReady)
            {
                AccumulateFrame(intensities);
                return null;
            }

            return Subtract(intensities);
        }

        public void Reset()
        {
            _accumulator = null;
            _baseline = null;
            _framesCollected = 0;
            _isReady = false;
        }

        private void AccumulateFrame(double[] intensities)
        {
            _accumulator ??= new double[intensities.Length];

            int length = Math.Min(intensities.Length, _accumulator.Length);
            for (int i = 0; i < length; i++)
            {
                _accumulator[i] += intensities[i];
            }

            _framesCollected++;

            if (_framesCollected >= FRAMES)
            {
                BuildBaseline();
            }
        }

        private void BuildBaseline()
        {
            _baseline = new double[_accumulator!.Length];
            double invN = 1.0 / _framesCollected;

            for (int i = 0; i < _accumulator.Length; i++)
            {
                _baseline[i] = _accumulator[i] * invN;
            }

            _accumulator = null;
            _isReady = true;
        }

        private double[] Subtract(double[] intensities)
        {
            int length = Math.Min(intensities.Length, _baseline!.Length);
            var result = new double[intensities.Length];

            for (int i = 0; i < length; i++)
            {
                result[i] = intensities[i] - _baseline[i];
            }

            return result;
        }
    }
}
