namespace OpticEMS.Preprocessing.Operations.Averaging
{
    public class MagneticFieldSmoother
    {
        private readonly double _targetIntervalMs;
        private readonly int _periodsToAverage;

        private Queue<(double Signal, double Timestamp)>[] _channelBuffers = Array.Empty<Queue<(double, double)>>();

        public MagneticFieldSmoother(double magneticFieldPeriodMs, int periodsToAverage = 5)
        {
            _targetIntervalMs = magneticFieldPeriodMs * periodsToAverage;
            _periodsToAverage = periodsToAverage;
        }

        public double ComputeAvg(double inputSignal, double elapsedMs)
            => ComputeAvg(inputSignal, elapsedMs, channelIndex: 0);

        public double ComputeAvg(double inputSignal, double elapsedMs, int channelIndex)
        {
            EnsureChannelCapacity(channelIndex + 1);

            var buffer = _channelBuffers[channelIndex];
            buffer.Enqueue((inputSignal, elapsedMs));

            while (buffer.Count > 0 && (elapsedMs - buffer.Peek().Timestamp > _targetIntervalMs))
            {
                buffer.Dequeue();
            }

            double accumulatedValue = 0;
            double totalWeight = 0;
            int i = 0;

            foreach (var frame in buffer)
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

            EnsureChannelCapacity(inputSignals.Length);

            var smoothed = new double[inputSignals.Length];

            for (int i = 0; i < inputSignals.Length; i++)
            {
                smoothed[i] = ComputeAvg(inputSignals[i], elapsedMs, channelIndex: i);
            }

            return smoothed;
        }

        public double[] Process(double[] inputs, double currentTimeMs)
            => ComputeAvg(inputs, currentTimeMs);

        public void Reset()
        {
            foreach (var buffer in _channelBuffers)
            {
                buffer.Clear();
            }
        }

        private void EnsureChannelCapacity(int requiredChannels)
        {
            if (_channelBuffers.Length >= requiredChannels)
            {
                return;
            }

            var newBuffers = new Queue<(double, double)>[requiredChannels];

            Array.Copy(_channelBuffers, newBuffers, _channelBuffers.Length);

            for (int i = _channelBuffers.Length; i < requiredChannels; i++)
            {
                newBuffers[i] = new Queue<(double, double)>();
            }

            _channelBuffers = newBuffers;
        }
    }
}