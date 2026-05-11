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
            List<double[]> framesToProcess;

            lock (_swapLock)
            {

                if (_writeBuffer.Count == 0)
                {
                    return Array.Empty<double>();
                }

                framesToProcess = _writeBuffer;
                _writeBuffer = new List<double[]>();

                /*
                var tmp = _readBuffer;
                _readBuffer = _writeBuffer;
                _writeBuffer = tmp;
                _writeBuffer.Clear();*/
            }
            /*
            int length = _readBuffer[0].Length;
            double[] sum = new double[length];

            foreach (var frame in _readBuffer)
            {
                for (int i = 0; i < length; i++) sum[i] += frame[i];
            }*/

            int frameCount = framesToProcess.Count;
            int dataLength = framesToProcess[0].Length;
            double[] avg = new double[dataLength];

            foreach (var frame in framesToProcess)
            {
                for (int i = 0; i < dataLength; i++)
                {
                    avg[i] += frame[i];
                }
            }

            for (int i = 0; i < dataLength; i++)
            {
                avg[i] /= frameCount;
            }

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
