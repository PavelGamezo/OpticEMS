using OpticEMS.Contracts.Services.Settings;
using Serilog;
using System.Runtime.InteropServices;

namespace OpticEMS.Devices.Devices.Avantes
{
    public class Avantes : Device
    {
        private int _devHandle = -1;
        private short _measuring = 1;
        private ushort _numPixels = 0;
        private double[] _wavelengths = Array.Empty<double>();

        private DeviceInfo Devices;

        public override DeviceInfo DeviceInfo
        {
            get => Devices;
            set
            {
                Devices = value;
            }
        }

        public Avantes()
        {
            Initialize(0);
        }

        public override void Initialize(int channelId)
        {
            int nrDevices = AvantesCCD.AVS_Init(0);

            if (nrDevices <= 0)
            {
                return;
            }

            uint requiredSize = 0;
            AvantesCCD.AvsIdentityType[] list = new AvantesCCD.AvsIdentityType[nrDevices];
            AvantesCCD.AVS_GetList((uint)(nrDevices * Marshal.SizeOf(typeof(AvantesCCD.AvsIdentityType))), ref requiredSize, list);

            _devHandle = AvantesCCD.AVS_Activate(ref list[0]);
            if (_devHandle == 1000)
            {
                Log.Error("[D:Avantes]: Failed to activate Device");
                throw new Exception("Failed to activate device");
            }

            AvantesCCD.AVS_GetNumPixels(_devHandle, ref _numPixels);
            AvantesCCD.DeviceConfigType config = new AvantesCCD.DeviceConfigType();
            uint structSize = (uint)Marshal.SizeOf(typeof(AvantesCCD.DeviceConfigType));
            int res = AvantesCCD.AVS_GetParameter(_devHandle, structSize, ref requiredSize, ref config);

            if (res == AvantesCCD.ERR_SUCCESS)
            {
                var c = config.m_Detector.m_aFit;

                Devices = new DeviceInfo(
                    list[0].m_SerialNumber,
                    _numPixels,
                    _devHandle,
                    channelId,
                    DeviceType.Avantes,
                    0,
                    0,
                    c[3], c[2], c[1], c[0],
                    0
                );
            }

            AvantesCCD.PixelArrayType lambda = new AvantesCCD.PixelArrayType();
            AvantesCCD.AVS_GetLambda(_devHandle, ref lambda);
            _wavelengths = lambda.Value.Take(_numPixels).ToArray();
        }

        public override bool Scan(int id, double[] collection, CancellationToken cancellationToken)
        {
            if (_devHandle == -1)
            {
                return false;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                int status = AvantesCCD.AVS_PollScan(_devHandle);

                if (status > 0)
                {
                    uint timeLabel = 0;
                    AvantesCCD.PixelArrayType spectrum = new AvantesCCD.PixelArrayType();
                    AvantesCCD.AVS_GetScopeData(_devHandle, ref timeLabel, ref spectrum);

                    for (int i = 0; i < _numPixels && i < collection.Length; i++)
                    {
                        collection[i] = spectrum.Value[i];
                    }

                    return true;
                }

                if (status < 0)
                {
                    return false;
                }
            }

            return false;
        }

        public override void SetParameters(int id, float exposureMs, int scansNum, int mode)
        {
            Log.Information($"[D:Avantes]: Setting parameters for {DeviceInfo.Name}: ExposureTime = {exposureMs}, ScansNum = {scansNum}, Mode = {mode}");
            var trigger = Convert.ToBoolean(mode);

            if (_devHandle == -1)
            {
                Log.Error($"[D:Avantes]: Device is not initialized");
                return;
            }

            AvantesCCD.MeasConfigType config = new AvantesCCD.MeasConfigType
            {
                m_StartPixel = 0,
                m_StopPixel = (ushort)(_numPixels - 1),
                m_IntegrationTime = exposureMs,
                m_NrAverages = (uint)scansNum,
                m_Trigger = new AvantesCCD.TriggerType
                {
                    m_Mode = AvantesCCD.SW_TRIGGER_MODE,
                    m_Source = AvantesCCD.EXT_TRIGGER_SOURCE
                }
            };

            if (trigger) // Continuous
            {
                _measuring = -1; 
                config.m_Trigger.m_Mode = AvantesCCD.SW_TRIGGER_MODE;
                config.m_Trigger.m_Source = AvantesCCD.SYNCH_TRIGGER_SOURCE;
            }

            Log.Information($"[D:Avantes]: Applying new parameters for device {DeviceInfo.Name}");

            int result = AvantesCCD.AVS_PrepareMeasure(_devHandle, ref config);
            if (result != AvantesCCD.ERR_SUCCESS) throw new Exception($"PrepareMeasure failed: {result}");
            
            AvantesCCD.AVS_Measure(_devHandle, IntPtr.Zero, _measuring);
        }

        public override void StopMeasurement()
        {
            Log.Information($"[D:Avantes]: Stopping measuring request for {DeviceInfo.Name}");
            if (_devHandle != -1)
            {
                var response = AvantesCCD.AVS_StopMeasure(_devHandle);
                Log.Information($"[D:Avantes]: Stopping measuring response with code {response}");

                if (AvantesCCD.ERR_SUCCESS != response)
                {
                    throw new Exception("Stopping measuring error");
                }
            }
        }
    }
}
