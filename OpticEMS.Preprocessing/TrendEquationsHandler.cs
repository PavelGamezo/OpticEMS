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

        public void PushIntensities(uint[] intensities)
        {
            _frameAverager.PushIntensities(intensities);
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

        public TrendResult Process(double elapsedMs)
        {
            var averagedFrame = _frameAverager.ComputeAveraged();
            if (averagedFrame == null || averagedFrame.Length == 0)
            {
                return TrendResult.Empty;
            }

            var smoothed = _mfSmoother.ComputeAvg(averagedFrame, elapsedMs);

            double[] derivatives = Array.Empty<double>();
            if (_useDerivating)
            {
                derivatives = _derivativeCalculator.ComputeDer(smoothed, elapsedMs);
            }

            return new TrendResult
            {
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
