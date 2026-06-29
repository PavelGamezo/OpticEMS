using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Common.Enums;
using OpticEMS.Contracts.Services.Settings;

namespace OpticEMS.MVVM.Models
{
    public partial class ChannelModel : ObservableObject
    {
        public int ChannelId { get; set; }

        [ObservableProperty]
        public List<string> availableSpectrometers;

        [ObservableProperty]
        private string selectedSpectrometer;

        [ObservableProperty]
        private SpectrometerType selectedSpectrometerType;

        [ObservableProperty]
        private string _coefficientA;

        [ObservableProperty]
        private string _coefficientB;

        [ObservableProperty]
        private string _coefficientC;

        [ObservableProperty]
        private string _coefficientD;

        [ObservableProperty]
        private double _trimLeft;

        [ObservableProperty]
        private double _trimRight;

        [ObservableProperty]
        private DeviceType deviceType = DeviceType.Unknown;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsYixistIpVisible))]
        private SpectrometerType _spectrometerTypeForVisibility;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsConnectEnabled))]
        private string _deviceIpAddress = string.Empty;

        [ObservableProperty] private string _yixistConnectionStatus = string.Empty;

        [ObservableProperty] private bool _isYixistConnecting;

        public bool IsYixistIpVisible => SelectedSpectrometerType == SpectrometerType.Yixist;

        public bool IsConnectEnabled =>
            SelectedSpectrometerType == SpectrometerType.Yixist &&
            !string.IsNullOrWhiteSpace(DeviceIpAddress) &&
            !IsYixistConnecting;

        partial void OnSelectedSpectrometerTypeChanged(SpectrometerType value)
        {
            OnPropertyChanged(nameof(IsYixistIpVisible));
            OnPropertyChanged(nameof(IsConnectEnabled));

            YixistConnectionStatus = string.Empty;
        }

        partial void OnDeviceIpAddressChanged(string value)
        {
            YixistConnectionStatus = string.Empty;
        }

        [ObservableProperty]
        private string _ip = "192.168.1.10";

        [ObservableProperty]
        private string _port = "502";
    }
}
