using System.Xml.Serialization;

namespace OpticEMS.Contracts.Services.Settings
{
    public class DeviceInfo
    {
        private double _coefA;
        private double _coefB;
        private double _coefC;
        private double _coefD;

        public DeviceInfo(string name, int pixelNum, int deviceId, int channelId, DeviceType deviceType, int trimLeft, int trimRiht, double coefA,
            double coefB, double coefC, double coefD, ushort smoothPixelCount)
        {
            Name = name;
            PixelNum = pixelNum;
            DeviceId = deviceId;
            ChannelId = channelId;
            TrimLeft = trimLeft;
            TrimRight = trimRiht;
            CoefA = coefA;
            CoefB = coefB;
            CoefC = coefC;
            CoefD = coefD;
            DeviceType = deviceType;
            Init();
            StartPixel = 0;
            EndPixel = pixelNum - 1;
        }

        public DeviceInfo()
        {
            Init();
        }

        public int DeviceId { get; set; }

        public int ChannelId { get; set; }

        public DeviceType DeviceType { get; set; }

        [XmlIgnore]
        public int LeftPixel { get; set; }

        [XmlIgnore]
        public int RightPixel { get; set; }

        public string Name { get; set; } = string.Empty;

        public int PixelNum { get; set; }

        private int _trimLeft;

        /// <summary>
        /// Device's lowest registered wavelength, nm.
        /// </summary>
        public int TrimLeft
        {
            get => _trimLeft;
            set => _trimLeft = value;
        }

        private int _trimRight;

        /// <summary>
        /// Device's highest registered wavelength, nm.
        /// </summary>
        public int TrimRight
        {
            get => _trimRight;
            set => _trimRight = value;
        }

        public int StartPixel { get; set; }

        public int EndPixel { get; set; }

        public double CoefA
        {
            get => _coefA;
            set => _coefA = value;
        }


        public double CoefB
        {
            get => _coefB;
            set => _coefB = value;
        }


        public double CoefC
        {
            get => _coefC;
            set => _coefC = value;
        }


        public double CoefD
        {
            get => _coefD;
            set => _coefD = value;
        }

        [XmlIgnore]
        public double[] Wavelengths { get; set; }

        [XmlIgnore]
        public double[] Signal { get; set; }

        public void Init()
        {
            DeviceType = DeviceType.VirtualSpec;
            Wavelengths = new double[PixelNum];
            Signal = new double[PixelNum];
        }

        public double GetWavelengths(int pixel)
        {
            var wavelength = CoefD
                    + CoefC * pixel
                    + CoefB * Math.Pow(pixel, 2)
                    + CoefA * Math.Pow(pixel, 3);

            return wavelength;
        }
    }
}
