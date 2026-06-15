namespace OpticEMS.Preprocessing.Operations.Averaging
{
    public class MagneticFieldSmoother
    {
        private readonly double _targetIntervalMs;
        private readonly Queue<(double Signal, double Timestamp)> _buffer = new();

        public MagneticFieldSmoother(double magneticFieldPeriodMs, int periodsToAverage = 5)
        {
            _targetIntervalMs = magneticFieldPeriodMs * periodsToAverage;
        }

        public double ComputeAvg(double inputSignal, double elapsedMs)
        {
            _buffer.Enqueue((inputSignal, elapsedMs));

            while (_buffer.Count > 0 && (elapsedMs - _buffer.Peek().Timestamp > _targetIntervalMs))
            {
                _buffer.Dequeue();
            }

            double accumulatedValue = 0;
            double totalWeight = 0;
            int i = 0;

            foreach (var frame in _buffer)
            {
                i++;
                double weight = i;
                totalWeight += weight;
                accumulatedValue += frame.Signal * weight;
            }

            return totalWeight > 0 ? (accumulatedValue / totalWeight) : inputSignal;
        }

        public double[] ComputeAvg(double[] inputSignals, double elapsedMs)
        {
            if (inputSignals == null || inputSignals.Length == 0)
            {
                return Array.Empty<double>();
            }

            var smoothed = new double[inputSignals.Length];

            for (int i = 0; i < inputSignals.Length; i++)
            {
                smoothed[i] = ComputeAvg(inputSignals[i], elapsedMs);
            }

            return smoothed;
        }

        public double[] Process(double[] inputs, double currentTimeMs)
        {
            var smoothedValues = ComputeAvg(inputs, currentTimeMs);

            return smoothedValues;
        }

        public void Reset() => _buffer.Clear();
    }
}
