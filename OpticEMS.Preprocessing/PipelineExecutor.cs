using OpticEMS.Contracts.Services.SignalPreprocessing;
using OpticEMS.Preprocessing.Operations.Averaging;
using OpticEMS.Preprocessing.Operations.Derivation;
using Serilog;

namespace OpticEMS.Preprocessing
{
    public class PipelineExecutor
    {
        private FrameAverager _frameAverager;
        private MagneticFieldSmoother _mfSmoother;
        private DerivativeCalculator _derivativeCalculator;

        private bool _useDerivating = false;

        public PipelineExecutor(bool useDerivating)
        {
            _useDerivating = useDerivating;

            Log.Information("[PREPROCESSING]: Initialized preprocessing handler");
        }

        public void PushIntensities(double[] intensities)
        {
            _frameAverager.PushIntensities(intensities);
        }

        public void Set(double magneticFieldPeriodMs = 2000, int mfPeriodsToAverage = 5, int derivationTime = 5)
        {
            try
            {
                _frameAverager = new FrameAverager();
                _mfSmoother = new MagneticFieldSmoother(magneticFieldPeriodMs, mfPeriodsToAverage);

                if (_useDerivating)
                {
                    _derivativeCalculator = new DerivativeCalculator(derivationTime);
                }

                Log.Information("[PREPROCESSING]: Preprocessing handler compiled");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[PREPROCESSING]: Error during setup preprocessing handler");
                throw;
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
                derivatives = _derivativeCalculator.Process(smoothed, elapsedMs);
            }

            return new TrendResult
            {
                FrameAveraged = averagedFrame,
                Smoothed = smoothed,
                Derivatives = derivatives,
                Timestamp = elapsedMs
            };
        }

        public void Reset()
        {
            _frameAverager.Reset();
            _mfSmoother.Reset();

            if (_derivativeCalculator != null)
            {
                _derivativeCalculator.Reset();
            }

            Log.Information("[PREPROCESSING]: Preprocessing handler reseted");
        }
    }
}
