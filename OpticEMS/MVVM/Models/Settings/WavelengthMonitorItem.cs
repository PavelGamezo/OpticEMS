using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace OpticEMS.MVVM.Models
{
    public partial class WavelengthMonitorItem : ObservableObject
    {
        [ObservableProperty]
        private double _wavelength;

        [ObservableProperty]
        private Color _color;

        [ObservableProperty] 
        private double _signalHigh;

        [ObservableProperty]
        private int _detectionWindowTime;

        [ObservableProperty]
        private int _windowInCount;

        [ObservableProperty]
        private int _windowOutCount;

        public WavelengthMonitorItem(
            double wavelength,
            Color color, 
            double signalHigh,
            int windowTime,
            int windowInCount,
            int windowOutCount)
        {
            Wavelength = wavelength;
            Color = color;
            SignalHigh = signalHigh;
            DetectionWindowTime = windowTime;
            WindowInCount = windowInCount;
            WindowOutCount = windowOutCount;
        }
    }
}
