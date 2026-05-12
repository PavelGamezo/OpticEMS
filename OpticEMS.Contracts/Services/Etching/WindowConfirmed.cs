namespace OpticEMS.Contracts.Services.Etching
{
    public record WindowConfirmed
    {
        public int WavelengthIndex { get; set; }

        // Координаты по оси времени (X)
        public double StartTime { get; set; } // В секундах
        public double EndTime { get; set; }   // StartTime + WindowWidth

        // Координаты по оси интенсивности (Y)
        public double Top { get; set; }       // Reference + WindowHeight
        public double Bottom { get; set; }    // Reference - WindowHeight
        public double Reference { get; set; } // Центральная линия окна

        public bool IsWindowIn { get; set; }
    }
}
