using OpticEMS.Contracts.Services.Settings;
using Serilog;
using System.Text;
using System.Timers;
using static OpticEMS.Devices.Devices.Yixis.YixistCCD;

namespace OpticEMS.Devices.Devices.Yixis
{
    internal class Yixist : Device
    {
        private UInt32 _port = 0;
        private UInt32 _deviceHandle;
        private readonly object @lock = new object();
        private bool _isInitialized;
        private string _serial;
        private int _pixels;
        private double _trimLeft;
        private double _trimRight;
        private bool _tecEnable;
        private ulong _time;
        private int _box;
        private int _avgTimes;
        private double integrationTimeMin;
        private double integrationTimeMax;
        private TriggerMode _triggerMode = TriggerMode.Default;

        public UInt32 Port => _port;
        public UInt32 DeviceHandle => _deviceHandle;
        public string SerialNumber => _serial;
        public int Pixels => _pixels;
        public double TrimLeft => _trimLeft;
        public double TrimRight => _trimRight;

        public Yixist(int id)
        {
            Initialize(id);
        }

        public override DeviceInfo DeviceInfo { get; set; }

        public override void Initialize(int id)
        {
            if (!_isInitialized)
            {
                Connect((uint)id);
                GetSN(ref _serial);
                GetPixels();
                GetWavelengthRange(ref _trimLeft, ref _trimRight);

                _isInitialized = true;

                DeviceInfo = new DeviceInfo(
                    SerialNumber, 
                    Pixels, 
                    (int)Port, 
                    0, DeviceType.Yixist, 
                    (int)TrimLeft, 
                    (int)TrimRight, 
                    0, 0, 0, 0, 0);
            }
        }

        public override bool Scan(int id, double[] collection, CancellationToken cancellationToken)
        {
            if (!_isInitialized)
            {
                return false;
            }

            lock (@lock)
            {
                try
                {
                    return ReadData(collection, 1);
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "[MD:Yixist] Error marshaling or calculating spectrum data.");
                    return false;
                }
            }
        }

        public override void SetParameters(int id, float exposureMs, int scansNum)
        {
            if (_deviceHandle == 0)
            {
                Log.Error($"[MD:Yixist]: Spectrometer {id} is not responding.");
                return;
            }

            Config((ulong)exposureMs, 1, scansNum, _triggerMode);
        }

        public override void StopMeasurement()
        {
            Reset();
        }

        private bool ReadData(double[] data, int avgTimes)
        {
            try
            {
                if (_deviceHandle == 0)
                {
                    Log.Error($"[MD:Yixist]: Device is not connected to  start reading data");
                    return false;
                }

                double[] buffer = new double[Pixels];
                int n = YixistCCD.SPReadDoubleCCDAvg(_deviceHandle, data, avgTimes);

                return n > 0;
            }
            catch (Exception ex)
            {
                return false;
            }

        }

        private bool GetSN(ref string sn)
        {
            byte[] _sn = new byte[256];
            bool r = YixistCCD.SPGetSN(_deviceHandle, _sn);
            if (r)
            {
                sn = Encoding.ASCII.GetString(_sn);
                TrimString(ref sn);
            }
            else
            {
                Log.Error($"[MD:Yixist]: Device is not responding for get serial number request");
                throw new Exception("Device is not responding for get serial number request");
            }

            return r;
        }

        private uint Connect(UInt32 port)
        {
            _deviceHandle = YixistCCD.SPConnect(port);
            if (_deviceHandle > 0)
            {
                _port = port;
                if (isSupportCooling())
                {
                    SetTecEnable(true);
                }
            }
            else
            {
                Log.Error($"[MD:Yixist]: Failed to activate Device");
                throw new Exception("Failder to activate Yixist device");
            }

            return _deviceHandle;
        }

        private bool SetTecEnable(bool en)
        {
            if (_tecEnable == en)
            {
                return true;
            }

            if (true == YixistCCD.SPSetTecEnable(_deviceHandle, en))
            {
                _tecEnable = en;
                return true;
            }
            return false;
        }

        private bool isSupportCooling()
        {
            List<int> coolingDetectorList = new List<int>() { 7, 8, 12, 18, 19 };
            int detectorId = GetDetectorId();
            return coolingDetectorList.Contains(detectorId);
        }

        public int GetDetectorId()
        {
            return YixistCCD.SPGetDetectorId(_deviceHandle);
        }

        private void TrimString(ref string str)
        {
            str = str.Replace('\r', '\0');
            str = str.Substring(0, str.IndexOf('\0'));
        }

        private bool Config(UInt64 integrateTime, int box = 0, int avg = 0, TriggerMode triggerMode = TriggerMode.NormalMode)
        {
            if (_deviceHandle == 0)
            {
                Log.Error($"[MD:Yixist]: Device is failed or disconnected");
                return false;
            }

            if (integrationTimeMin == 0)
            {
                ulong min = 0, max = 0;
                if (false == GetIntegrationTimeRange(ref min, ref max))
                {
                    return false;
                }
            }

            if (integrateTime > integrationTimeMax)
            {
                return false;
            }

            UInt64 minStep = 0;
            if (integrationTimeMin < 100)
            {
                minStep = 10;
            }
            else if (integrationTimeMin <= 500)
            {
                minStep = 100;
            }
            else
            {
                minStep = 1000;
            }

            integrateTime -= integrateTime % minStep;
            if (YixistCCD.SPConfig(_deviceHandle, integrateTime, box, (int)triggerMode))
            {
                _time = integrateTime;
                _box = box;
                _avgTimes = avg;
                _triggerMode = triggerMode;

                return true;
            }

            return false;
        }

        private bool GetIntegrationTimeRange(ref UInt64 min, ref UInt64 max)
        {
            bool timeInfoRequest = SPGetIntegrateTimeRange(_deviceHandle, ref min, ref max);
            if (timeInfoRequest)
            {
                integrationTimeMin = min;
                integrationTimeMax = max;
            }

            return timeInfoRequest;
        }

        private bool GetWavelengthRange(ref double min, ref double max)
        {
            var requestResult = YixistCCD.SPGetWaveLengthRange(_deviceHandle, ref min, ref max);
            if (!requestResult)
            {
                Log.Error($"[MD:Yixist]: Failed to activate Device");
                throw new Exception("Failder to activate Yixist device");
            }

            return requestResult;
        }

        public bool Reset()
        {
            return YixistCCD.SPReset(_deviceHandle);
        }

        public uint GetPixels()
        {
            uint nTotalPixel = 0, nStartPixel = 0, intensity = 0, total = 0;
            var ccdInfoRequest = YixistCCD.SPGetCCDInfo(_deviceHandle, ref nTotalPixel, ref nStartPixel, ref intensity);
            if (ccdInfoRequest)
            {
                total = nTotalPixel - nStartPixel;
                _pixels = (int)total;
            }
            else
            {
                Log.Error($"[MD:Yixist]: Failed to get device info request");
                throw new Exception("Failder to activate Yixist device");
            }

            return total;
        }
    }
}
