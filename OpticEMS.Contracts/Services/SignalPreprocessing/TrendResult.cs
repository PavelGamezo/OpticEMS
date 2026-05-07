namespace OpticEMS.Contracts.Services.SignalPreprocessing
{
    public class TrendResult()
    {
        public double[] FrameAveraged { get; init; } = Array.Empty<double>();
        public double[] Smoothed { get; init; } = Array.Empty<double>();
        public double[] Derivatives { get; init; } = Array.Empty<double>();
        public double Timestamp { get; init; }

        public static TrendResult Empty => new TrendResult();
    }
}
