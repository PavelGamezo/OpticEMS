using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Devices;
using OpticEMS.Devices.Devices.Avantes;
using OpticEMS.Devices.Devices.Solar;
using System.Runtime.InteropServices;

namespace OpticEMS.Services.Spectrometers
{
    public class SpectrometerService : ISpectrometerService
    {
        private readonly ISettingsProvider _configProvider;

        private readonly object _lock = new();
        private bool _isInitialized;
        private int _lastDeviceId = -1;
        private List<DeviceInfo> _foundDevices = new();

        public SpectrometerService(ISettingsProvider configProvider)
        {
            _configProvider = configProvider;

            InitializeLibrary();
        }

        public void RequestSingleScan(int cameraId)
        {
            var proc = new DeviceProcessing(cameraId, _configProvider);

            Task.Run(() => proc.StartSingleScan(cameraId, 5f, 1, CancellationToken.None));
        }

        private void InitializeLibrary()
        {
            if (_isInitialized)
            {
                return;
            }

            lock (_lock)
            {
                if (_isInitialized)
                {
                    return;
                }

                _isInitialized = SolarCCD.CCD_Init(IntPtr.Zero, null, ref _lastDeviceId);
            }
        }

        public int GetConnectedSpectrometersCount()
        {
            int total = 0;

            for (int i = 0; i < 15; i++)
            {
                if (SolarCCD.CCD_HitTest(i))
                {
                    total++;
                }
            }

            int avantesCount = AvantesCCD.AVS_Init(0);
            if (avantesCount > 0)
            {
                total += avantesCount;
            }

            return total;
        }

        public string? GetSerialNumber(int cameraId)
        {
            if (_foundDevices.Count == 0)
            {
                RefreshDeviceList();
            }

            if (cameraId >= 0 && cameraId < _foundDevices.Count)
            {
                return _foundDevices[cameraId].Name;
            }

            return null;
        }

        private void RefreshDeviceList()
        {
            _foundDevices.Clear();

            for (int i = 0; i < 15; i++)
            {
                if (SolarCCD.CCD_HitTest(i))
                {
                    IntPtr ptr = SolarCCD.CCD_GetSerialNumber(i);
                    string sn = ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : $"Solar-{i}";
                    _foundDevices.Add(new DeviceInfo { Name = sn, DeviceId = i, DeviceType = DeviceType.Solar });
                }
            }

            int avsCount = AvantesCCD.AVS_Init(0);
            if (avsCount > 0)
            {
                uint reqSize = 0;
                var list = new AvantesCCD.AvsIdentityType[avsCount];
                AvantesCCD.AVS_GetList((uint)(avsCount * Marshal.SizeOf(typeof(AvantesCCD.AvsIdentityType))), ref reqSize, list);

                foreach (var avs in list)
                {
                    _foundDevices.Add(new DeviceInfo { Name = avs.m_SerialNumber, DeviceType = DeviceType.Avantes });
                }
            }
        }

        public uint[]? GetSpectrometerData(int cameraId, float exposureMs)
        {
            if (!SolarCCD.CCD_HitTest(cameraId)) return null;

            lock (_lock)
            {
                if (!SolarCCD.CCD_SetParameter(cameraId, SolarCCD.PRM_EXPTIME, exposureMs))
                {
                    return null;
                }

                float pixelsF = 0;
                if (!SolarCCD.CCD_GetParameter(cameraId, SolarCCD.PRM_NUMPIXELS, ref pixelsF) || pixelsF <= 0)
                {
                    SolarCCD.CCD_GetParameter(cameraId, SolarCCD.PRM_NUMPIXELS, ref pixelsF);
                }

                int pixels = (int)pixelsF;
                if (pixels <= 0)
                {
                    pixels = 2048;
                }

                if (!SolarCCD.CCD_InitMeasuring(cameraId))
                {
                    return null;
                }

                IntPtr buffer = Marshal.AllocHGlobal(pixels * sizeof(uint));

                try
                {
                    if (SolarCCD.CCD_StartWaitMeasuring(cameraId))
                    {
                        if (SolarCCD.CCD_GetData(cameraId, buffer))
                        {
                            uint[] result = new uint[pixels];
                            int[] temp = new int[pixels];
                            Marshal.Copy(buffer, temp, 0, pixels);
                            return Array.ConvertAll(temp, x => (uint)x);
                        }
                    }

                    return null;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        public bool IsSpectrometerInitialized() => _isInitialized;
    }
}
