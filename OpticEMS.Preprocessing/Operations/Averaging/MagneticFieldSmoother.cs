using OpticEMS.Contracts.Services.SignalPreprocessing;

namespace OpticEMS.Preprocessing.Operations.Averaging
{
    public class MagneticFieldSmoother : ISignalOperation
    {
        private readonly double _periodMs;
        private readonly int _periodsToAverage;
        private readonly Queue<double[]> _mfBuffer = new();
        private double _lastUpdateTime = 0;

        public MagneticFieldSmoother(double magneticFieldPeriodMs, int periodsToAverage = 1)
        {
            _periodMs = magneticFieldPeriodMs;
            _periodsToAverage = Math.Max(1, periodsToAverage);
        }

        public string Name => "Magnetic Field Smoother";

        public string Description => $"MF smoothing over {_periodsToAverage} periods";

        public uint ComputeAvg(uint value)
        {
            throw new NotImplementedException();
        }

        public uint[] ComputeAvg(uint[] inputSignal, double elapsedMs)
        {
            if (inputSignal == null || inputSignal.Length == 0)
            {
                return Array.Empty<uint>();
            }

            double interval = _periodMs / _periodsToAverage;

            if (elapsedMs - _lastUpdateTime >= interval)
            {
                _mfBuffer.Enqueue(Array.ConvertAll(inputSignal, x => (double)x));

                if (_mfBuffer.Count > _periodsToAverage)
                {
                    _mfBuffer.Dequeue();
                }

                _lastUpdateTime = elapsedMs;
            }

            if (_mfBuffer.Count == 0)
            {
                return (uint[])inputSignal.Clone();
            }

            int length = inputSignal.Length;
            double[] sum = new double[length];

            foreach (var frame in _mfBuffer)
            {
                for (int i = 0; i < length; i++)
                {
                    sum[i] += frame[i];
                }
            }

            uint[] smoothed = new uint[length];
            for (int i = 0; i < length; i++)
            {
                smoothed[i] = (uint)(sum[i] / _mfBuffer.Count);
            }

            return smoothed;
        }

        /// <summary>
        /// Not implemented in this operation version
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public double ComputeDer(uint value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not implemented in this operation version
        /// </summary>
        /// <param name="values"></param>
        /// <param name="elapsedMs"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public double[] ComputeDer(uint[] values, double elapsedMs)
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            _mfBuffer.Clear();
            _lastUpdateTime = 0;
        }
    }
}
