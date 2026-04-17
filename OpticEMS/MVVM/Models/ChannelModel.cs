using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Contracts.Services.Settings;

namespace OpticEMS.MVVM.Models
{
    public partial class ChannelModel : ObservableObject
    {
        public int ChannelId { get; set; }

        public List<string> AvailableSpectrometers { get; set; }

        [ObservableProperty]
        private string selectedSpectrometer;

        [ObservableProperty]
        private SpectrometerType selectedSpectrometerType;

        [ObservableProperty]
        private string calibrationCoefficientsString;

        [ObservableProperty]
        private double _trimLeft;

        [ObservableProperty]
        private double _trimRight;

        [ObservableProperty]
        private DeviceType deviceType = DeviceType.Unknown;
    }
}
