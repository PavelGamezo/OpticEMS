using OpticEMS.Contracts.Services.Settings;
using Serilog;
using System.Text;
using static OpticEMS.Devices.Devices.Yixis.YixistCCD;

namespace OpticEMS.Devices.Devices.Yixis
{
    public class Yixist : Device
    {
        private readonly string _ip;
        private readonly int _tcpPort;

        private UInt32 _deviceHandle;
        private readonly object @lock = new object();
        private bool _isInitialized;
        private string _serial = string.Empty;
        private int _pixels;
        private double _trimLeft;
        private double _trimRight;
        private double _coefA;
        private double _coefB;
        private double _coefC;
        private double _coefD;
        private bool _tecEnable;
        private ulong _time;
        private int _box;
        private int _avgTimes;
        private ulong _integrationTimeMin;
        private ulong _integrationTimeMax;

        private TriggerMode _triggerMode = TriggerMode.NormalMode;

        public UInt32 DeviceHandle => _deviceHandle;
        public string SerialNumber => _serial;
        public int Pixels => _pixels;
        public double TrimLeft => _trimLeft;
        public double TrimRight => _trimRight;

        public override DeviceInfo DeviceInfo { get; set; }

        public Yixist(string ip, int tcpPort = 8080)
        {
            _ip = ip;
            _tcpPort = tcpPort;
            Initialize(0);
        }

        public override void Initialize(int channelId)
        {
            if (_isInitialized)
            {
                return;
            }

            ConnectTCP();
            ReadSN();
            ReadPixels();
            ReadWavelengthRange();
            ReadCalibrationCoefficients();
            //SetHighGain();

            _isInitialized = true;

            DeviceInfo = new DeviceInfo(
                _serial,
                _pixels,
                (int)_deviceHandle,
                channelId,
                DeviceType.Yixist,
                (int)_trimLeft,
                (int)_trimRight,
                _coefA, _coefB, _coefC, _coefD, 0)
            {
                DeviceIp = _ip
            };
        }

        public override bool Scan(int id, double[] collection, CancellationToken cancellationToken)
        {
            try
            {
                if (!_isInitialized || _deviceHandle == 0)
                {
                    Log.Warning("[D:Yixist]: Scan called but device is not initialized.");
                    return false;
                }

                lock (@lock)
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        int avg = Math.Max(1, _avgTimes);
                        int n = YixistCCD.SPReadDoubleCCDAvg(_deviceHandle, collection, avg);
                        if (n <= 0)
                        {
                            Log.Warning("[D:Yixist]: SPReadDoubleCCDAvg returned {N} for {Name}", n, DeviceInfo?.Name);
                        }

                        return n > 0;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[D:Yixist]: Error reading spectrum data for {Name}.", DeviceInfo?.Name);
                
                return false;
            }
        }

        public override void SetParameters(int id, float exposureMs, int scansNum, float equalizer, int mode = 0)
        {
            Log.Information(
                "[D:Yixist]: SetParameters for {Name}: exposure={Exp}ms, scans={Scans}, mode={Mode}",
                DeviceInfo?.Name, exposureMs, scansNum, mode);

            if (_deviceHandle == 0)
            {
                Log.Error("[D:Yixist]: Cannot set parameters — device not connected.");
                return;
            }

            _triggerMode = mode == 1 ? TriggerMode.NormalMode : TriggerMode.SoftwareTriggerMode;
            _avgTimes = Math.Max(1, scansNum);
            _box = (int)equalizer;
            ulong exposureUs = Math.Max(_integrationTimeMin > 0 ? _integrationTimeMin : 4000,
                                (ulong)(exposureMs * 1000));

            bool ok = Config(exposureUs, _box, _avgTimes, _triggerMode);

            Log.Information("[D:Yixist]: Config result={Ok}, exposureUs={Us}", ok, exposureUs);
        }

        public override void StopMeasurement()
        {
            Log.Information("[D:Yixist]: StopMeasurement for {Name}.", DeviceInfo?.Name);
            lock (@lock)
            {
                if (_deviceHandle != 0)
                {
                    YixistCCD.SPConfig(_deviceHandle, 4000, 0, (int)TriggerMode.NormalMode);
                }
            }
        }

        private void ConnectTCP()
        {
            _deviceHandle = YixistCCD.SPConnectTCP(_ip, _tcpPort);

            if (_deviceHandle > 0)
            {
                Log.Information("[D:Yixist]: Connected via TCP to {IP}:{Port}, handle={Handle}",
                    _ip, _tcpPort, _deviceHandle);

                if (IsSupportCooling())
                    SetTecEnable(true);
            }
            else
            {
                Log.Error("[D:Yixist]: Failed to connect via TCP to {IP}:{Port}", _ip, _tcpPort);
            }
        }

        private void Reset()
        {
            var result = YixistCCD.SPClose(_deviceHandle);
        }

        //private void SetHighGain()
        //{
        //    bool ok = YixistCCD.SPSetConversionEfficiency(_deviceHandle, 1);
        //    Log.Information("[D:Yixist]: High gain set on init, result={Ok}", ok);
        //}

        private void TryResetBeforeCalibration()
        {
            try
            {
                bool ok = YixistCCD.SPReset(_deviceHandle);
                if (ok)
                {
                    Thread.Sleep(200);
                    Log.Information("[D:Yixist]: Reset before calibration read succeeded.");
                }
                else
                {
                    Log.Warning("[D:Yixist]: Reset returned false — proceeding anyway.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[D:Yixist]: Reset before calibration failed — proceeding.");
            }
        }

        private void ReadSN()
        {
            byte[] buf = new byte[256];
            if (!YixistCCD.SPGetSN(_deviceHandle, buf))
            {
                Log.Error("[D:Yixist]: Failed to read serial number from {IP}.", _ip);
            }
            _serial = TrimBytes(buf);
        }

        private void ReadPixels()
        {
            uint total = 0, start = 0, backlight = 0;
            if (!YixistCCD.SPGetCCDInfo(_deviceHandle, ref total, ref start, ref backlight))
            {
                Log.Error("[D:Yixist]: Failed to get CCD info from {IP}.", _ip);
            }
            _pixels = (int)(total - start);
        }

        private void ReadWavelengthRange()
        {
            if (!YixistCCD.SPGetWaveLengthRange(_deviceHandle, ref _trimLeft, ref _trimRight))
            {
                Log.Error("[D:Yixist]: Failed to get wavelength range from {IP}.", _ip);
            }
        }

        private bool Config(ulong integrateTime, int box = 0, int avg = 0,
            TriggerMode triggerMode = TriggerMode.NormalMode)
        {
            if (_integrationTimeMin == 0)
            {
                ulong mn = 0, mx = 0;
                if (!GetIntegrationTimeRange(ref mn, ref mx))
                {
                    return false;
                }
            }

            if (integrateTime > _integrationTimeMax)
            {
                Log.Warning("[D:Yixist]: Integration time {T}µs exceeds max {Max}µs",
                    integrateTime, _integrationTimeMax);
                return false;
            }

            ulong step = _integrationTimeMin < 100 ? 10u
                       : _integrationTimeMin <= 500 ? 100u
                       : 1000u;

            integrateTime -= integrateTime % step;
            _time = integrateTime;

            if (!YixistCCD.SPConfig(_deviceHandle, integrateTime, box, (int)triggerMode))
            {
                return false;
            }

            return true;
        }

        private bool GetIntegrationTimeRange(ref ulong min, ref ulong max)
        {
            bool ok = YixistCCD.SPGetIntegrateTimeRange(_deviceHandle, ref min, ref max);
            if (ok)
            {
                _integrationTimeMin = min;
                _integrationTimeMax = max;
            }

            return ok;
        }

        private bool IsSupportCooling()
        {
            var ids = new List<int> { 7, 8, 12, 18, 19 };
            return ids.Contains(YixistCCD.SPGetDetectorId(_deviceHandle));
        }

        private bool SetTecEnable(bool en)
        {
            if (_tecEnable == en)
            {
                return true;
            }

            if (YixistCCD.SPSetTecEnable(_deviceHandle, en))
            {
                _tecEnable = en;
                return true;
            }

            return false;
        }

        private void ReadCalibrationCoefficients()
        {
            double[] C = new double[4];
            if (!YixistCCD.SPGetCalData(_deviceHandle, C))
            {
                Log.Warning("[D:Yixist]: Failed to read cal data from {IP}, using defaults.", _ip);
                return;
            }

            _coefD = C[0];
            _coefC = C[1];
            _coefB = C[2];
            _coefA = C[3];

            Log.Information("[D:Yixist]: CalData from device {IP}: C0={C0}, C1={C1}, C2={C2}, C3={C3}",
                _ip, C[0], C[1], C[2], C[3]);
        }

        private static string TrimBytes(byte[] buf)
        {
            string s = Encoding.ASCII.GetString(buf).Replace('\r', '\0');
            int idx = s.IndexOf('\0');

            return idx >= 0 ? s[..idx] : s;
        }
    }
}