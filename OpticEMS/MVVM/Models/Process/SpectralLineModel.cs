using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Notifications.Messages;
using System.Windows.Media;

namespace OpticEMS.MVVM.Models.Process
{
    public partial class SpectralLineModel : ObservableObject
    {
        private readonly int _channelId;

        public SpectralLineModel(int channelId)
        {
            _channelId = channelId;
        }

        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _element;

        [ObservableProperty]
        private string _ionization;

        [ObservableProperty]
        private double _wavelength;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LineBrush))]
        private string _colorHex = "#3498DB";

        [ObservableProperty]
        private bool _isSelected;

        public string IconChar => !string.IsNullOrEmpty(Element) ? Element.Substring(0, 1) : "?";

        public SolidColorBrush LineBrush => new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(ColorHex ?? "#3498DB"));

        public Color LineColor => 
            (Color)ColorConverter.ConvertFromString(ColorHex ?? "#3498DB");

        partial void OnIsSelectedChanged(bool value)
        {
            WeakReferenceMessenger.Default.Send(
                new SpectralLineSelectionMessage(_channelId, Wavelength, ColorHex));
        }
    }
}
