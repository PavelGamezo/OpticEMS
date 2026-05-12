namespace OpticEMS.Contracts.Services.Export
{
    public class TimePoint
    {
        public double TimeSeconds { get; set; }

        public double[] Trend { get; set; } = Array.Empty<double>();
        
        public double[] Preprocessed { get; set; } = Array.Empty<double>(); 
        
        public double[] Processed { get; set; } = Array.Empty<double>();    
    }
}
