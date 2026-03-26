using System.Runtime.InteropServices;
using System.Text;

namespace OpticEMS.Devices.Devices.Solar
{
    public class SolarCCD
    {
        #region fields

        public const int PRM_DEVICEPROPERTY = 15;
        public const int PRM_EXPTIME = 5;
        public const int PRM_READOUTS = 4;
        public const int PRM_SYNCHR = 6;
        public const int PRM_NUMPIXELS = 3;
        public const int PRM_DEVICEMODE = 10;
        public const int DEVICEMODES = 3; // Спектроскопический режим
        public const int SYNCHR_NONE = 1; // Без синхронизации
        public const uint STATUS_DATA_READY = 3; // Пример, уточните по PDF

        private const string CCD_PATH = @"D:\Работа\Izovac\SDK\x64\";
        private const string CCD_DLL = CCD_PATH + "CCDUSBDCOM01.dll";

        #endregion

        #region structs

        [StructLayout(LayoutKind.Sequential)]
        public struct TCCDUSBExtendParams
        {
            public uint dwDigitCapacity;
            public int nPixelRate;
            public int nNumPixelsH;
            public int nNumPixelsV;
            public uint Reserve1;
            public uint Reserve2;
            public int nNumReadOuts;
            public float sPreBurning;
            public float sExposureTime;
            public float sTime2;
            public uint dwSynchr;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bSummingMode;
            public uint dwDeviceMode;
            public int nStripCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)] // MAXSTRIPS=10
            public RECT[] rcStrips;
            public int Reserve11;
            public uint dwSensitivity;
            public uint dwProperty;
            public float sShutterTime;
            public uint Reserve6;
            public uint Reserve7;
            public uint Reserve8;
            public uint Reserve9;
            public uint Reserve10;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        #endregion

        #region importDLL

        // Инициализация (подключение к SDK)
        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CCD_Init(IntPtr ahAppWnd, string Prm, ref int ID);

        // Для C# (скорее всего) не нужен
        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CCD_GetSerialNum(
            int ID,
            StringBuilder sernum);

        // Получаю серийный номер по ID
        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern IntPtr CCD_GetSerialNumber(int ID);

        // Обратная операция - получаю ID по серийному номеру
        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CCD_GetID(string sernum, ref int ID);

        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CCD_HitTest(int ID);

        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CCD_GetData(int ID, IntPtr pData);

        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CCD_InitMeasuringData(int id, IntPtr pData);

        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CCD_SetParameter(int id, uint prmId, float value);

        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CCD_InitMeasuring(int ID);

        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CCD_StartWaitMeasuring(int ID);

        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CCD_StartMeasuring(int ID);

        // СБРОС КАМЕРЫ - прерывает текущую регистрацию и инициализирует камеру
        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CCD_CameraReset(int ID);

        // ПОЛУЧЕНИЕ ПАРАМЕТРА - получает значение параметра камеры
        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CCD_GetParameter(int ID, uint dwPrmID, ref float Prm);

        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CCD_GetExtendParameters(int ID, ref TCCDUSBExtendParams Prms);

        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CCD_ClearStrips(int ID);

        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CCD_AddStrip(int ID, RECT arcStrip);

        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CCD_SetExtendParameters(int ID, ref TCCDUSBExtendParams Prms);

        [DllImport(CCD_DLL, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CCD_GetMeasureStatus(int ID, ref uint adwStatus);

        #endregion
    }
}
