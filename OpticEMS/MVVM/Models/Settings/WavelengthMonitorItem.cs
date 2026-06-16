using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Contracts.Services.Database;
using System.Windows.Media;

namespace OpticEMS.MVVM.Models
{
    public partial class WavelengthMonitorItem : ObservableObject
    {
        [ObservableProperty]
        private double _wavelength;

        [ObservableProperty]
        private SpectralLine? _selectedLine;

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

        private bool _isSync;

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

        partial void OnSelectedLineChanged(SpectralLine? value)
        {
            if (_isSync)
            {
                return;
            }

            if (value != null)
            {
                _isSync = true;
                Wavelength = value.Wavelength;
                _isSync = false;
            }
        }
        partial void OnWavelengthChanged(double value)
        {
            if (_isSync)
            {
                return;
            }

            _isSync = true;
            if (SelectedLine != null && Math.Abs(SelectedLine.Wavelength - value) > 0.01)
            {
                SelectedLine = null;
            }
            _isSync = false;
        }
    }
}
