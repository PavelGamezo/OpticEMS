using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace OpticEMS.MVVM.Models
{
    public partial class WavelengthMonitorItem : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private double _wavelength;

        [ObservableProperty]
        private Color _color;

        [ObservableProperty] 
        private int _signalHigh;

        public WavelengthMonitorItem(string name, double wavelength, Color color, int signalHigh)
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"CH{Wavelength}" : name;
            Wavelength = wavelength;
            Color = color;
            SignalHigh = signalHigh;
        }
    }
}
