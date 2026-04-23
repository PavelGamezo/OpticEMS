namespace OpticEMS.Contracts.Services.Export
{
    public class TimePoint
    {
        public double TimeSeconds { get; set; }

        public List<uint> Intensities { get; set; } = new();    }
}
