using OpticEMS.Contracts.Services.ProcessingModes;
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

        public ProcessingMode ProcessingMode { get; set; } = ProcessingMode.SingleChannel;
        public DualChannelSubMode DualSubMode { get; set; } = DualChannelSubMode.Simultaneous;

        public List<string> WavelengthNames { get; set; } = new();
        public List<double> Wavelengths { get; set; } = new();
        public List<Color> WavelengthColors { get; set; } = new();

        public List<double> DetectionWindowHighs { get; set; } = new();
        public int DetectionWindowTime { get; set; }

        public float MagneticFieldPeriodMs { get; set; }
        public int FieldPeriodsToAverage { get; set; }

        public bool DerivativeEnabled { get; set; }
        public int DerivativePoints { get; set; } = 1;

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

        public string ProcessingModeDisplay => ProcessingMode switch
        {
            ProcessingMode.SingleChannel => "Single Channel",
            ProcessingMode.DualChannel => DualSubMode switch
            {
                DualChannelSubMode.Simultaneous => "Dual Channel (Simultaneous)",
                DualChannelSubMode.Ratio => "Dual Channel (Ratio)",
                _ => "Dual Channel"
            },
            _ => "Unknown Mode"
        };

        public string ActiveModeShort => ProcessingMode switch
        {
            ProcessingMode.SingleChannel => "Single",
            ProcessingMode.DualChannel => DualSubMode == DualChannelSubMode.Ratio ? "Ratio" : "Dual",
            _ => "Unknown"
        };

        public bool IsRatioMode => ProcessingMode == ProcessingMode.DualChannel &&
                                   DualSubMode == DualChannelSubMode.Ratio;

        public void AutoConfigureMode()
        {
            if (Wavelengths.Count <= 1)
            {
                ProcessingMode = ProcessingMode.SingleChannel;
                DualSubMode = DualChannelSubMode.Simultaneous;
            }
            else if (Wavelengths.Count >= 2)
            {
                ProcessingMode = ProcessingMode.DualChannel;
                if (DualSubMode != DualChannelSubMode.Ratio)
                {
                    DualSubMode = DualChannelSubMode.Simultaneous;
                }
            }
        }

        public bool CanUseRatioMode => Wavelengths.Count == 2;
    }
}
