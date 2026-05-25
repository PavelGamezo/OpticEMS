using OpticEMS.Contracts.Preprocessing;
using Serilog;

namespace OpticEMS.Preprocessing.Operations.Averaging
{
    public class MagneticFieldSmoother : INodeProcessor
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

        public double Process(double[] inputs, double currentTimeMs)
        {
            if (inputs == null || inputs.Length == 0 || inputs[0] == null)
            {
                return 0;
            }

            double currentSingleValue = inputs[0];

            double smoothedValue = ComputeAvg(currentSingleValue, currentTimeMs);

            return smoothedValue;
        }

        public void Reset() => _buffer.Clear();
    }
}
