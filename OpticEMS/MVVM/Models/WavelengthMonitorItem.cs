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
        private int _signalHigh;

        public WavelengthMonitorItem(double wavelength, Color color, int signalHigh)
        {
            Wavelength = wavelength;
            Color = color;
            SignalHigh = signalHigh;
        }
    }
}
