using OpticEMS.Contracts.Services.Recipe;
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
            double periodMs = _periodMs;
            int avgCount = Math.Max(1, _periodsToAverage);

            if (elapsedMs - _lastUpdateTime >= periodMs / avgCount)
            {
                _mfBuffer.Enqueue(Array.ConvertAll(inputSignal, x => (double)x));
                if (_mfBuffer.Count > avgCount)
                {
                    _mfBuffer.Dequeue();
                }

                _lastUpdateTime = elapsedMs;
            }

            if (_mfBuffer.Count == 0)
            {
                return inputSignal;
            }

            var averaged = new uint[inputSignal.Length];
            foreach (var frame in _mfBuffer)
            {
                for (int i = 0; i < inputSignal.Length; i++)
                {
                    averaged[i] += (uint)(frame[i] / _mfBuffer.Count);
                }
            }

            return averaged;
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
