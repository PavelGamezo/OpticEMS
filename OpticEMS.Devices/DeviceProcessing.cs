using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Devices.Devices.Avantes;
using OpticEMS.Devices.Devices.Solar;
using OpticEMS.Devices.Devices.VirtualSpec;
using OpticEMS.Notifications.Messages;

namespace OpticEMS.Devices
{
    public class DeviceProcessing
    {
        private ISettingsProvider _configProvider;

        private Device? _device;

        private DeviceType _deviceType;

        private bool _scanning;

        public int ChannelId { get; private set; }

        public float ExposureTime { get; private set; }

        public int ScanNum { get; private set; }

        public double[] Wavelengths { get; set; }

        public uint[] Intensities { get; set; }

        public DeviceProcessing(int channelId, ISettingsProvider configProvider)
        {
            ChannelId = channelId;
            _configProvider = configProvider;

            var saved = _configProvider.GetByChannelId(channelId);

            _deviceType = saved.DeviceType;
            int realDeviceId = saved.DeviceId;

            switch (_deviceType)
            {
                case DeviceType.Solar:
                    _device = new Solar(saved.Name);
                    break;
                case DeviceType.Avantes:
                    _device = new Avantes();
                    break;
                case DeviceType.VirtualSpec:
                    _device = new VirtualSpec(realDeviceId);
                    break;
                default:
                    _device = new VirtualSpec(realDeviceId);
                    break;
            }

            ApplyCalibrationFromConfig();
        }

        private void ApplyCalibrationFromConfig()
        {
            if (_configProvider.GetByChannelId(ChannelId) is null)
            {
                return;
            }

            var saved = _configProvider.GetByChannelId(ChannelId);
            if (saved == null)
            {
                return;
            }

            _device!.DeviceInfo = saved;
        }

        public bool InitWavelengths()
        {
            if (_device is null)
            {
                return false;
            }

            Wavelengths = new double[_device.DeviceInfo.PixelNum];
            Intensities = new uint[_device.DeviceInfo.PixelNum];

            for (int i = 0; i < _device.DeviceInfo.PixelNum; i++)
            {
                Wavelengths[i] = _device.DeviceInfo.GetWavelengths(i);
            }

            return true;
        }

        public void StartContinueScan(float exposureMs, int scanNum, CancellationToken cancellationToken)
        {
            ExposureTime = exposureMs;
            ScanNum = scanNum;
            var deviceId = _device.DeviceInfo.DeviceId;

            SetParameters(deviceId, ExposureTime, ScanNum);
            InitWavelengths();

            _scanning = true;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!_device.Scan(deviceId, Intensities, cancellationToken))
                    {
                        break;
                    }

                    WeakReferenceMessenger.Default.Send(new SpectrumUpdatedMessage(ChannelId, Intensities, Wavelengths));
                }
            }
            finally
            {
                _scanning = false;
            }
        }

        public void StartSingleScan(int id, float exposureMs, int scanNum, CancellationToken cancellationToken)
        {
            _scanning = true;
            ExposureTime = exposureMs;
            ScanNum = scanNum;

            SetParameters(id, ExposureTime, ScanNum);
            InitWavelengths();

            if (!_device.Scan(id, Intensities, cancellationToken))
            {
                return;
            }

            WeakReferenceMessenger.Default.Send(new CalibrationChartUpdatedMessage(id, Intensities));
        }

        private void SetParameters(int id, float exposureMs, int scansNum)
        {
            if (_device is null)
            {
                throw new Exception("Can't set device parameters because of device init error.");
            }

            _device?.SetParameters(id, exposureMs, scansNum);
        }

        public void NotifyVirtualProcessStarted()
        {
            if (_device is VirtualSpec vSpec)
            {
                vSpec.StartProcess();
            }
        }

        public void NotifyVirtualProcessPaused()
        {
            if (_device is VirtualSpec vSpec)
            {
                vSpec.PauseProcess();
            }
        }

        public void NotifyVirtualProcessStopped()
        {
            if (_device is VirtualSpec vSpec)
            {
                vSpec.StopProcess();
            }
        }
    }
}
