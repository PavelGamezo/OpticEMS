using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Contracts.Services.Settings;

namespace OpticEMS.MVVM.Models
{
    public partial class ChannelModel : ObservableObject
    {
        [ObservableProperty] 
        private int _channelId;

        [ObservableProperty] 
        private string? _selectedSpectrometer;

        [ObservableProperty]
        private DeviceType deviceType = DeviceType.Unknown;

        public List<string> AvailableSpectrometers { get; set; } = new();
    }
}
