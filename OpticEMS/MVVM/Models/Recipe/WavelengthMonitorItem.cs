using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Notifications.Messages.SpectralLines;
using System.Windows.Media;

namespace OpticEMS.Contracts.Services.Recipe
{
    public partial class WavelengthMonitorItem : ObservableObject
    {
        [ObservableProperty]
        private double _wavelength;

        [ObservableProperty]
        private bool _isAddFormVisible = false;

        [ObservableProperty]
        private string _newElement = string.Empty;

        [ObservableProperty]
        private string _newWavelengthText = string.Empty;

        [ObservableProperty]
        private Color _newLineColor = Colors.Cyan;

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


        [RelayCommand]
        private void ShowAddSpectralLinePanel()
        {
            NewElement = string.Empty;
            NewWavelengthText = string.Empty;
            NewLineColor = Colors.Cyan;
            IsAddFormVisible = true;
        }

        [RelayCommand]
        private void CloseAddForm()
        {
            IsAddFormVisible = false;
        }

        [RelayCommand]
        private void SaveNewLine()
        {
            string hexColor = $"#{NewLineColor.R:X2}{NewLineColor.G:X2}{NewLineColor.B:X2}";
            WeakReferenceMessenger.Default.Send(new SaveSpectralLineMessage(NewElement, NewWavelengthText, hexColor));

            NewElement = string.Empty;
            NewWavelengthText = string.Empty;
            NewLineColor = Colors.Cyan;
            IsAddFormVisible = false;
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
