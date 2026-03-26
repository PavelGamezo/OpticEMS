using OpticEMS.Contracts.Services.Settings;
using System.Runtime.InteropServices;

namespace OpticEMS.Devices.Devices.Solar
{
    public class Solar : Device
    {
        private static readonly object @lock = new object();

        private bool _isInitialized;
        private IntPtr _ahAppWnd;
        private int _totalDevices;
        private int _deviceId;


        private DeviceInfo Devices;

        public override DeviceInfo DeviceInfo
        {
            get => Devices;
            set
            {
                lock (@lock)
                {
                    Devices = value;
                }
            }
        }

        public Solar(int channelId)
        {
            Initialize(channelId);
        }

        public Solar(string serialNumber)
        {
            InitializeBySerialNumber(serialNumber);
        }

        public static bool IsPresent(int id)
        {
            try
            {
                int tempId = id;

                var result = SolarCCD.CCD_Init(IntPtr.Zero, "", ref tempId);

                return result;
            }
            catch { return false; }
        }

        private void InitializeBySerialNumber(string serialNumber)
        {
            if (_isInitialized)
            {
                return;
            }
            lock (@lock)
            {
                _ahAppWnd = new IntPtr();
                SolarCCD.CCD_Init(_ahAppWnd, "", ref _totalDevices);
                _isInitialized = true;
                for (int i = 0; i < _totalDevices; i++)
                {
                    var n1 = Marshal.PtrToStringAnsi(SolarCCD.CCD_GetSerialNumber(i));
                    if (n1 == serialNumber)
                    {
                        _deviceId = i;
                        var p1 = new SolarCCD.TCCDUSBExtendParams();
                        SolarCCD.CCD_GetExtendParameters(_deviceId, ref p1);
                        var p = p1.nNumPixelsH * p1.nNumPixelsV;
                        Devices = new DeviceInfo(n1, p, _deviceId, 0, DeviceType.Solar, 0, p - 1, 0, 0, 0, 0, 0);
                        break;
                    }
                }
            }
        }

        public override void Initialize(int channelId)
        {
            if (_isInitialized)
            {
                return;
            }

            lock (@lock)
            {
                _ahAppWnd = new IntPtr();
                SolarCCD.CCD_Init(_ahAppWnd, "", ref _totalDevices);
                _isInitialized = true;

                var n1 = Marshal.PtrToStringAnsi(SolarCCD.CCD_GetSerialNumber(_deviceId));

                var p1 = new SolarCCD.TCCDUSBExtendParams();
                SolarCCD.CCD_GetExtendParameters(_deviceId, ref p1);
                var p = p1.nNumPixelsH * p1.nNumPixelsV;

                lock (@lock)
                {
                    Devices = new DeviceInfo(n1, p, _deviceId, channelId, DeviceType.Solar ,10, 0, 0, 0, 0, 0, 0);
                }
            }
        }

        public override bool Scan(int cameraId, uint[] collection, CancellationToken cancellationToken)
        {
            if (!_isInitialized)
            {
                return false;
            }

            lock (@lock)
            {
                int pixels = collection.Length; 
                IntPtr buffer = Marshal.AllocHGlobal(pixels * sizeof(uint));

                try
                {
                    if (!SolarCCD.CCD_InitMeasuring(cameraId)) return false;

                    if (SolarCCD.CCD_StartWaitMeasuring(cameraId))
                    {
                        if (cancellationToken.IsCancellationRequested) return false;

                        if (SolarCCD.CCD_GetData(cameraId, buffer))
                        {
                            int[] temp = new int[pixels];
                            Marshal.Copy(buffer, temp, 0, pixels);

                            for (int i = 0; i < pixels; i++)
                                collection[i] = (uint)temp[i];

                            return true;
                        }
                    }

                    return false;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        public override void SetParameters(int id, float exposureMs, int scansNum)
        {
            SolarCCD.CCD_SetParameter(id, SolarCCD.PRM_EXPTIME, exposureMs);
        }

        public override void StopMeasurement()
        {
            throw new NotImplementedException();
        }
    }
}
