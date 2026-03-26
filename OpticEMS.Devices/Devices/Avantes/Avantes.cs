using OpticEMS.Contracts.Services.Settings;
using System.Runtime.InteropServices;

namespace OpticEMS.Devices.Devices.Avantes
{
    public class Avantes : Device
    {
        private int _devHandle = -1;
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
                throw new Exception("Failed to activate Avantes device");
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

        public override bool Scan(int id, uint[] collection, CancellationToken cancellationToken)
        {
            if (_devHandle == -1)
            {
                return false;
            }

            AvantesCCD.AVS_Measure(_devHandle, IntPtr.Zero, 1);

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
                        collection[i] = (uint)spectrum.Value[i];
                    }

                    return true;
                }

                if (status < 0)
                {
                    return false;
                }

                Thread.Sleep(1);
            }

            return false;
        }

        public override void SetParameters(int id, float exposureMs, int scansNum)
        {
            if (_devHandle == -1) return;

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

            int result = AvantesCCD.AVS_PrepareMeasure(_devHandle, ref config);
            if (result != AvantesCCD.ERR_SUCCESS) throw new Exception($"PrepareMeasure failed: {result}");
        }

        public override void StopMeasurement()
        {
            if (_devHandle != -1)
            {
                AvantesCCD.AVS_StopMeasure(_devHandle);
            }
        }
    }
}
