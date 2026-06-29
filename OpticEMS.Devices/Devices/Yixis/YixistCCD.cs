using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;

namespace OpticEMS.Devices.Devices.Yixis
{
    public static class YixistCCD
    {
        public enum TriggerMode
        {
            /// <summary>
            /// Normal mode
            /// </summary>
            NormalMode = 0x11,
            /// <summary>
            /// Software trigger
            /// </summary>
            SoftwareTriggerMode = 0x12,
            /// <summary>
            /// Hardware trigger
            /// </summary>
            HardwareTriggerMode = 0x13,
            /// <summary>
            /// Synchronous trigger
            /// </summary>
            SyncTriggerMode = 0x14,
            /// <summary>
            /// Passing in 0 will not change the original trigger mask parameter.
            /// </summary>
            Default = 0
        }

        public struct Color_Result
        {
            public double m_dx;
            public double m_dy;
            public double m_dz;
            public double m_dBigX;
            public double m_dBigY;
            public double m_dBigZ;
            public double m_du;
            public double m_dv;
            public double m_duPrime; //           CIE1976UV - u
            public double m_dvprime; //        -   CIE1976UV - v
            public double m_dwprime; //        -   CIE1976UV

            public double m_dPeakWavelength;
            public double m_dPeakIntensity;
            public double m_dDominantWavelength;
            public double m_Pe;
            public double m_dColorTemperature;

            public double m_dRa;
            public double m_dR1;
            public double m_dR2;
            public double m_dR3;
            public double m_dR4;
            public double m_dR5;
            public double m_dR6;
            public double m_dR7;
            public double m_dR8;
            public double m_dR9;
            public double m_dR10;
            public double m_dR11;
            public double m_dR12;
            public double m_dR13;
            public double m_dR14;

            public double Hunter_L;
            public double Hunter_a;
            public double Hunter_b;
            public double CIE_L;
            public double CIE_a;
            public double CIE_b;
            public double Cab; //Chroma
            public double Hab; //Hue angle
            public double YI;
            public double Rratio; //Red ratio
            public double fCTA; //Color tolerance
        };

        private const string CCD_DLL = "Libraries\\spectrometer.dll";

        #region importDll

        [DllImport(CCD_DLL, EntryPoint = "SPGetAllDevices", CharSet = CharSet.Auto)]
        public static extern UInt32 SPGetAllDevices([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] UInt32[] port, out UInt32 num);

        [DllImport(CCD_DLL, EntryPoint = "SPConnect", CharSet = CharSet.Auto)]
        public static extern UInt32 SPConnect(UInt32 port);

        [DllImport(CCD_DLL, EntryPoint = "SPConnectTCP", CharSet = CharSet.Ansi)]
        public static extern UInt32 SPConnectTCP(string ip, int tcp_port);

        [DllImport(CCD_DLL, EntryPoint = "SPConfig", CharSet = CharSet.Auto)]
        public static extern bool SPConfig(UInt32 DevID, UInt64 dwIntegrateTime, int BoxCar, int TriggerMode);

        [DllImport(CCD_DLL, EntryPoint = "SPReadDoubleCCD", CharSet = CharSet.Auto)]
        public static extern int SPReadDoubleCCD(UInt32 DevID, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] double[] data);

        [DllImport(CCD_DLL, EntryPoint = "SPReadDoubleCCDAvg", CharSet = CharSet.Auto)]
        public static extern int SPReadDoubleCCDAvg(UInt32 DevID, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] double[] data, int avgTimes);

        [DllImport(CCD_DLL, EntryPoint = "SPGetCalData", CharSet = CharSet.Auto)]
        public static extern bool SPGetCalData(UInt32 DevID, [MarshalAs(UnmanagedType.LPArray, SizeConst = 4)] double[] C);

        [DllImport(CCD_DLL, EntryPoint = "SPWaveLengthToPixel", CharSet = CharSet.Auto)]
        public static extern int SPWaveLengthToPixel(UInt32 DevID, double fWaveLen);

        [DllImport(CCD_DLL, EntryPoint = "SPPixelToWaveLength", CharSet = CharSet.Auto)]
        public static extern double SPPixelToWaveLength(UInt32 DevID, UInt32 pixelIndex);

        [DllImport(CCD_DLL, EntryPoint = "SPClose", CharSet = CharSet.Auto)]
        public static extern bool SPClose(UInt32 DevID);

        [DllImport(CCD_DLL, EntryPoint = "SPStopACQ", CharSet = CharSet.Auto)]
        public static extern bool SPStopACQ(UInt32 DevID);

        [DllImport(CCD_DLL, EntryPoint = "GetDllVer", CharSet = CharSet.Auto)]
        public static extern bool GetDllVer(UInt32 DevID, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data);

        [DllImport(CCD_DLL, EntryPoint = "SPGetDeviceModelName", CharSet = CharSet.Auto)]
        public static extern bool SPGetDeviceModelName(UInt32 DevID, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] modelName);


        [DllImport(CCD_DLL, EntryPoint = "SPGetSN", CharSet = CharSet.Auto)]
        public static extern bool SPGetSN(UInt32 DevID, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] SN);

        [DllImport(CCD_DLL, EntryPoint = "SPGetFirmWareVer", CharSet = CharSet.Auto)]
        public static extern bool SPGetFirmWareVer(UInt32 DevID, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] firmwareVer);


        [DllImport(CCD_DLL, EntryPoint = "SPGetWaveLengthRange", CharSet = CharSet.Auto)]
        public static extern bool SPGetWaveLengthRange(UInt32 DevID, ref double min, ref double max);

        [DllImport(CCD_DLL, EntryPoint = "SPGetResolution", CharSet = CharSet.Auto)]
        public static extern bool SPGetResolution(UInt32 DevID, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] resolution);


        [DllImport(CCD_DLL, EntryPoint = "SPGetSlit", CharSet = CharSet.Auto)]
        public static extern bool SPGetSlit(UInt32 DevID, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] slit);

        [DllImport(CCD_DLL, EntryPoint = "SPGetIntegrateTimeRange", CharSet = CharSet.Auto)]
        public static extern bool SPGetIntegrateTimeRange(UInt32 DevID, ref UInt64 min, ref UInt64 max);

        [DllImport(CCD_DLL, EntryPoint = "SPGetCCDInfo", CharSet = CharSet.Auto)]
        public static extern bool SPGetCCDInfo(UInt32 DevID, ref uint nTotalPixel, ref uint nStartPixel, ref uint BackLightIntensity);

        [DllImport(CCD_DLL, EntryPoint = "SPGetADDigits", CharSet = CharSet.Auto)]
        public static extern int SPGetADDigits(UInt32 DevID);

        [DllImport(CCD_DLL, EntryPoint = "SPGetDetectorId", CharSet = CharSet.Auto)]
        public static extern int SPGetDetectorId(UInt32 DevID);

        [DllImport(CCD_DLL, EntryPoint = "SPReset", CharSet = CharSet.Auto)]
        public static extern bool SPReset(UInt32 DevID);

        [DllImport(CCD_DLL, EntryPoint = "SPSetStandardLampSpectrum", CharSet = CharSet.Auto)]
        public static extern bool SPSetStandardLampSpectrum(UInt32 DevID, double[] wave, double[] pow, int cnt, double T);

        [DllImport(CCD_DLL, EntryPoint = "SPProcessSpectrum", CharSet = CharSet.Auto)]
        public static extern Color_Result SPProcessSpectrum(UInt32 DevID, double[] wave, double[] pow, int cnt);

        [DllImport(CCD_DLL, EntryPoint = "SPSetConversionEfficiency", CharSet = CharSet.Auto)]
        public static extern bool SPSetConversionEfficiency(UInt32 DevID, byte stat);

        [DllImport(CCD_DLL, EntryPoint = "SPSetTecEnable", CharSet = CharSet.Auto)]
        public static extern bool SPSetTecEnable(UInt32 DevID, bool enable);

        [DllImport(CCD_DLL, EntryPoint = "SPGetTecEnableState", CharSet = CharSet.Auto)]
        public static extern bool SPGetTecEnableState(UInt32 DevID, ref bool enable);

        [DllImport(CCD_DLL, EntryPoint = "SPSetDetectorTemperature", CharSet = CharSet.Auto)]
        public static extern bool SPSetDetectorTemperature(UInt32 DevID, float temperature);

        [DllImport(CCD_DLL, EntryPoint = "SPReadSetDetectorTemperature", CharSet = CharSet.Auto)]
        public static extern bool SPReadSetDetectorTemperature(UInt32 DevID, ref float temperature);

        [DllImport(CCD_DLL, EntryPoint = "SPReadDetectorTemperature", CharSet = CharSet.Auto)]
        public static extern bool SPReadDetectorTemperature(UInt32 DevID, ref float temperature);


        [DllImport(CCD_DLL, EntryPoint = "SPGetDeviceType", CharSet = CharSet.Auto)]
        public static extern bool SPGetDeviceType(UInt32 DevID, ref UInt16 deviceType);

        #endregion
    }
}
