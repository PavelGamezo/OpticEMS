using System.Windows.Media;

namespace OpticEMS.Contracts.Services.Recipe
{
    public class Recipe
    {
        /// <summary>
        /// Unique identifier of the recipe stored in the database.
        /// </summary>
        public int DatabaseId { get; set; }
        public int RecipeId { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<string> WavelengthNames { get; set; } = new();
        public List<double> Wavelengths { get; set; } = new();
        public List<Color> WavelengthColors { get; set; } = new();

        public List<double> DetectionWindowHighs { get; set; } = new();
        public int DetectionWindowTime { get; set; }

        public string GraphJson { get; set; } = string.Empty;

        public int WindowInCount { get; set; } = 1;
        public int WindowOutCount { get; set; } = 1;

        public int InitialDelay { get; set; }
        public int MaxEndpointTime { get; set; }

        public bool AutocalibrationEnabled { get; set; }
        public bool OverEtchEnabled { get; set; }
        public bool PcaEnabled { get; set; }

        public int OverEtchValue { get; set; }

        public int PcaComponents { get; set; } = 5;
        public int PcaMinTrainingSize { get; set; } = 150;

        public DateTime CreatedAt { get; set; }
        public DateTime LastModifiedAt { get; set; }
    }
}
