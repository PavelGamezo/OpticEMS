using OpticEMS.Contracts.Services.SignalPreprocessing;
using OpticEMS.Preprocessing.Operations.Averaging;
using OpticEMS.Preprocessing.Operations.Derivation;

namespace OpticEMS.Preprocessing
{
    public class TrendEquationsHandler
    {
        private FrameAverager _frameAverager;
        private MagneticFieldSmoother _mfSmoother;
        private DerivativeCalculator _derivativeCalculator;

        private bool _useDerivating = false;
        private bool _useMFAveraging = false;

        public TrendEquationsHandler(bool useDerivating)
        {
            _useDerivating = useDerivating;
        }

        public void Set(double magneticFieldPeriodMs = 2000, int mfPeriodsToAverage = 1, int derivationTime = 5)
        {
            _frameAverager = new FrameAverager();
            _mfSmoother = new MagneticFieldSmoother(magneticFieldPeriodMs, mfPeriodsToAverage);

            if (_useDerivating)
            {
                _derivativeCalculator = new DerivativeCalculator(derivationTime);
            }
        }

        public TrendResult Process(uint[] rawIntensities, double elapsedMs)
        {
            if (rawIntensities == null || rawIntensities.Length == 0)
            {
                return TrendResult.Empty;
            }

            var averagedFrame = _frameAverager.ComputeAvg(rawIntensities, elapsedMs);

            uint[] smoothed = averagedFrame;
            if (_useMFAveraging)
            {
                smoothed = _mfSmoother.ComputeAvg(averagedFrame, elapsedMs);
            }

            double[] derivatives = Array.Empty<double>();
            if (_useDerivating)
            {
                derivatives = _derivativeCalculator.ComputeDer(smoothed, elapsedMs);
            }

            return new TrendResult
            {
                Raw = rawIntensities,
                FrameAveraged = averagedFrame,
                Smoothed = smoothed,
                Derivatives = derivatives,
                Timestamp = elapsedMs
            };
        }

        public void EnableMFAveraging(bool enable) => _useMFAveraging = enable;
        public void EnableDerivative(bool enable) => _useDerivating = enable;

        public void Reset()
        {
            _frameAverager.Reset();
            _mfSmoother.Reset();
            _derivativeCalculator.Reset();
        }
    }
}
