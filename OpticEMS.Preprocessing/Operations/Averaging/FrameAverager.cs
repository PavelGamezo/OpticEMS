namespace OpticEMS.Preprocessing.Operations.Averaging
{
    public class FrameAverager
    {
        private readonly object _swapLock = new();
        private List<double[]> _writeBuffer = new();
        private List<double[]> _readBuffer = new();

        private double[] _lastAveraged = Array.Empty<double>();

        public string Name => "Frame Averaging";

        public string Description => $"Frame averaging over frames";

        public double[] ComputeAveraged()
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

            double[] avg = new double[length];
            for (int i = 0; i < length; i++)
            {
                avg[i] = (sum[i] / _readBuffer.Count);
            }

            _lastAveraged = avg;

            return avg;
        }

        public void PushIntensities(double[] currentIntensities)
        {
            lock (_swapLock)
            {
                _writeBuffer.Add((double[])currentIntensities.Clone());
            }
        }

        public void Reset()
        {
            _readBuffer.Clear();
            _writeBuffer.Clear();
            _lastAveraged = Array.Empty<double>();
        }
    }
}
