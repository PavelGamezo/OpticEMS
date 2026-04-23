using System.Windows.Media;

namespace OpticEMS.Contracts.Services.Recipe
{
    public class Recipe
    {
        public string Name { get; set; } = string.Empty;

        public int Channel { get; set; }

        public string ProcessChamber { get; set; } = string.Empty;

        public List<double> Wavelengths { get; set; } = new();

        public List<Color> WavelengthColors { get; set; } = new();

        public int InitialDelay { get; set; }

        public List<int> DetectionWindowHighs { get; set; } = new();

        public int DetectionWindowTime { get; set; }

        public float ExposureMs { get; set; } = 1;

        public int ScansNum { get; set; } = 1;

        public int WindowInCount { get; set; } = 1;

        public int WindowOutCount { get; set; } = 1;

        public int StableThresholdPercent { get; set; } = 1;

        public int MaxEndpointTime { get; set; }

        public bool AutocalibrationEnabled { get; set; }

        public bool OverEtchEnabled { get; set; }

        public int OverEtchValue { get; set; }

        public bool PCAEnabled { get; set; }

        public int PCAComponents { get; set; } = 5;

        public DateTime CreatedAt { get; set; }

        public DateTime LastModifiedAt { get; set; }
    }
}
