namespace OpticEMS.Contracts.Services.SignalPreprocessing
{
    public class TrendResult()
    {
        public uint[] FrameAveraged { get; init; } = Array.Empty<uint>();
        public uint[] Smoothed { get; init; } = Array.Empty<uint>();
        public double[] Derivatives { get; init; } = Array.Empty<double>();
        public double Timestamp { get; init; }

        public static TrendResult Empty => new TrendResult();
    }
}
