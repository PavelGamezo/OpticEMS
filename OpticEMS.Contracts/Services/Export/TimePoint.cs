namespace OpticEMS.Contracts.Services.Export
{
    public class TimePoint
    {
        public double TimeSeconds { get; set; }

        public uint[] Intensities { get; set; } = Array.Empty<uint>();    
    }
}
