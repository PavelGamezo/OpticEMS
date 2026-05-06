using OpticEMS.Contracts.Services.SignalPreprocessing;

namespace OpticEMS.Preprocessing.Operations.Averaging
{
    public class FrameAverager : ISignalOperation
    {
        private readonly object _swapLock = new();
        private List<uint[]> _writeBuffer = new();
        private List<uint[]> _readBuffer = new();

        private uint[] _lastAveraged = Array.Empty<uint>();

        public string Name => "Frame Averaging";

        public string Description => $"Frame averaging over frames";

        /// <summary>
        /// Not implemented in this operation version
        /// </summary>
        /// <param name="currentFrame"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public uint ComputeAvg(uint currentFrame)
        {
            throw new NotImplementedException();
        }

        public uint[] ComputeAvg(uint[] currentFrame, double elapsedMs)
        {
            lock (_swapLock)
            {
                if (_writeBuffer.Count == 0)
                {
                    return _lastAveraged;
                }

                var tmp = _readBuffer;
                _readBuffer = _writeBuffer;
                _writeBuffer = tmp;
                _writeBuffer.Clear();
            }

            int length = _readBuffer[0].Length;
            double[] sum = new double[length];

            foreach (var frame in _readBuffer)
            {
                for (int i = 0; i < length; i++) sum[i] += frame[i];
            }

            uint[] avg = new uint[length];
            for (int i = 0; i < length; i++)
            {
                avg[i] = (uint)(sum[i] / _readBuffer.Count);
            }

            _lastAveraged = avg;

            return avg;
        }

        public void PushIntensities(uint[] currentIntensities)
        {
            lock (_swapLock)
            {
                _writeBuffer.Add((uint[])currentIntensities.Clone());
            }
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
            _readBuffer.Clear();
            _writeBuffer.Clear();
            _lastAveraged = Array.Empty<uint>();
        }
    }
}
