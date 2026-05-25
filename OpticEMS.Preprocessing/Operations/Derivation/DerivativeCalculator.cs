using OpticEMS.Contracts.Preprocessing;

namespace OpticEMS.Preprocessing.Operations.Derivation
{
    public class DerivativeCalculator : INodeProcessor
    {
        private readonly int _derivationTime;
        private readonly Queue<double> _buffer = new();
        private double _lastValue = 0;
        private bool _isInitialized = false;

        public DerivativeCalculator(int derivationTime = 5)
        {
            if (derivationTime < 2)
            {
                throw new ArgumentException("Derivation Time must be at least 2", nameof(derivationTime));
            }

            _derivationTime = derivationTime;
        }

        public double ComputeDer(double currentValue)
        {
            _buffer.Enqueue(currentValue);

            if (_buffer.Count > _derivationTime)
            {
                _buffer.Dequeue();
            }

            if (_buffer.Count < 2)
            {
                _lastValue = currentValue;
                return 0;
            }

            var values = _buffer.ToArray();
            int n = values.Length;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

            for (uint i = 0; i < n; i++)
            {
                var x = i;
                var y = values[i];
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 1e-10)
            {
                return 0;
            }

            var slope = (n * sumXY - sumX * sumY) / denominator;

            _lastValue = currentValue;
            return slope;
        }

        public double[] ComputeDer(double[] currentValues)
        {
            if (currentValues == null || currentValues.Length == 0)
            {
                return Array.Empty<double>();
            }

            var derivatives = new double[currentValues.Length];

            for (int i = 0; i < currentValues.Length; i++)
            {
                derivatives[i] = ComputeDer(currentValues[i]);
            }

            return derivatives;
        }

        public void Reset()
        {
            _buffer.Clear();
            _lastValue = 0;
            _isInitialized = false;
        }

        public double Process(double[] inputs, double currentTimeMs)
        {
            var currentValues = inputs[0];

            return ComputeDer(currentValues);
        }
    }
}
