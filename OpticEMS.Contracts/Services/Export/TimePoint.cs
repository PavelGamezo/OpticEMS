namespace OpticEMS.Contracts.Services.Export
{
    public class TimePoint
    {
        public double TimeSeconds { get; set; }

        public double[] Intensities { get; set; } = Array.Empty<double>();    
    }
}
