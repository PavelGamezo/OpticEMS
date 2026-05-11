namespace OpticEMS.Preprocessing.Operations.Averaging
{
    public class MagneticFieldSmoother
    {
        private readonly double _targetIntervalMs;
        private readonly Queue<(double[] Signal, double Timestamp)> _buffer = new();

        public MagneticFieldSmoother(double magneticFieldPeriodMs, int periodsToAverage = 1)
        {
            _targetIntervalMs = magneticFieldPeriodMs * periodsToAverage;
        }

        public double[] ComputeAvg(double[] inputSignal, double elapsedMs)
        {
            _buffer.Enqueue(((double[])inputSignal.Clone(), elapsedMs));

            while (_buffer.Count > 0 && (elapsedMs - _buffer.Peek().Timestamp > _targetIntervalMs))
            {
                _buffer.Dequeue();
            }

            int len = inputSignal.Length;
            var result = new double[len];
            double totalWeight = 0;

            int i = 0;
            foreach (var frame in _buffer)
            {
                i++;
                double weight = i;
                totalWeight += weight;

                for (int j = 0; j < len; j++)
                {
                    result[j] += frame.Signal[j] * weight;
                }
            }

            for (int j = 0; j < len; j++)
            {
                result[j] /= totalWeight;
            }

            return result;
        }

        public void Reset() => _buffer.Clear();
    }
}
