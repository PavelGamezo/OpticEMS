using OpticEMS.Contracts.Services.Settings;
using Serilog;
using Serilog.Core;
using System.Runtime.InteropServices;
using System.Windows.Media.Media3D;

namespace OpticEMS.Devices.Devices.Solar
{
    public class Solar : Device
    {
        private static readonly object @lock = new object();

        private bool _isInitialized;
        private IntPtr _ahAppWnd;
        private IntPtr _pData;
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
                        Devices = new DeviceInfo(n1, p, _deviceId, 0, DeviceType.Solar, 0, 0, 0, 0, 0, 0, 0);
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
                    Devices = new DeviceInfo(n1, p, _deviceId, channelId, DeviceType.Solar, 10, 0, 0, 0, 0, 0, 0);
                }
            }
        }

        public override bool Scan(int cameraId, double[] collection, CancellationToken cancellationToken)
        {
            if (!_isInitialized)
            {
                return false;
            }

            lock (@lock)
            {
                if (_pData == IntPtr.Zero)
                {
                    Log.Error($"[MD:Solar]: Scan failed because of zero memory buffer.");
                    return false;
                }

                int pixelCount = DeviceInfo.PixelNum;
                var ps = new SolarCCD.TCCDUSBExtendParams();

                if (!SolarCCD.CCD_GetExtendParameters(cameraId, ref ps))
                {
                    Log.Error($"[MD:Solar]: Failed to get parameters for Device {cameraId}");
                    return false;
                }

                int scansNum = ps.nNumReadOuts <= 0 ? 1 : ps.nNumReadOuts;
                int totalElements = pixelCount * scansNum;

                try
                {
                    if (!SolarCCD.CCD_StartMeasuring(cameraId))
                    {
                        Log.Error($"[MD:Solar]: Start measuring error");
                        return false;
                    }

                    var startTime = DateTime.Now;
                    uint status = 0;

                    do
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return false;
                        }

                        if ((DateTime.Now - startTime).TotalSeconds > 20.0)
                        {
                            Log.Error($"[MD:Solar]: Spectrometer {cameraId} is not responding (Timed out waiting for data)");
                            SolarCCD.CCD_CameraReset(cameraId);
                            return false;
                        }

                        if (!SolarCCD.CCD_GetMeasureStatus(cameraId, ref status))
                        {
                            Log.Error($"[MD:Solar] Error calling status for Device {DeviceInfo.Name}");
                            return false;
                        }
                    } while (status != SolarCCD.STATUS_DATA_READY);

                    int[] tmpInt = new int[totalElements];

                    Marshal.Copy(_pData, tmpInt, 0, tmpInt.Length);

                    int elementsToCopy = Math.Min(collection.Length, pixelCount);
                    for (int i = 0; i < elementsToCopy; i++)
                    {
                        collection[i] = 0d;
                    }

                    for (int ii = 0; ii < elementsToCopy; ii++)
                    {
                        for (int kk = 0; kk < scansNum; kk++)
                        {
                            collection[ii] += tmpInt[ii + kk * pixelCount];
                        }
                    }


                    int adcRes = (int)ps.dwDigitCapacity;
                    if (adcRes <= 0) adcRes = 16;

                    int di = scansNum * (int)Math.Pow(2, adcRes - 14);
                    if (di > 1)
                    {
                        for (int i = 0; i < elementsToCopy; i++)
                        {
                            collection[i] /= di;
                        }
                    }

                    return true;
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "[MD:Solar] Error marshaling or calculating spectrum data.");
                    return false;
                }
            }
        }

        public override void SetParameters(int id, float exposureMs, int scansNum, int mode)
        {
            SolarCCD.CCD_SetParameter(id, SolarCCD.PRM_EXPTIME, exposureMs);
            var prms = new SolarCCD.TCCDUSBExtendParams();

            if (!SolarCCD.CCD_GetExtendParameters(id, ref prms))
            {
                Log.Error($"[MD:Solar]: Failed to get parameters for Device {DeviceInfo.Name}");
            }
            prms.sExposureTime = exposureMs;
            prms.nNumReadOuts = scansNum;
            prms.dwSynchr = SolarCCD.SYNCHR_NONE;
            prms.dwDeviceMode = 1;

            if (!SolarCCD.CCD_SetExtendParameters(id, ref prms))
            {
                throw new Exception($"Driver rejected parameters for Device {DeviceInfo.Name}.");
            }
            int pixelCount = DeviceInfo.PixelNum;
            int bufferSizeInBytes = sizeof(uint) * pixelCount * scansNum;

            lock (@lock)
            {
                if (_pData != IntPtr.Zero)
                {
                    SolarCCD.CCD_DoneMeasuring(id);
                    Marshal.FreeHGlobal(_pData);
                    _pData = IntPtr.Zero;
                }

                _pData = Marshal.AllocHGlobal(bufferSizeInBytes);
            }

            if (!SolarCCD.CCD_HitTest(id))
            {
                Log.Error($"[MD:Solar] HitTest failure");
                throw new Exception($"Spectrometer {DeviceInfo.Name} is not ready.");
            }

            if (!SolarCCD.CCD_InitMeasuring(id))
            {
                Log.Error($"[MD:Solar] Init measuring data failure");
                throw new Exception($"Spectrometer {id} is not ready.");
            }

            if (!SolarCCD.CCD_InitMeasuringData(id, _pData))
            {
                Log.Error($"[MD:Solar] Init measuring data failure");
                throw new Exception($"Spectrometer {id} is not ready.");
            }

            Log.Information($"[MD:Solar] Spectrometer is ready for measuring.");
        }

        public override void StopMeasurement()
        {
            Thread.Sleep(1);
            var result = SolarCCD.CCD_CameraReset(_deviceId);
        }
    }
}
