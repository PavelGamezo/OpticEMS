using OpticEMS.Contracts.Services.Settings;
using System.Reflection.Metadata;
using static OpticEMS.Devices.Devices.Avantes.AvantesCCD;

namespace OpticEMS.Common.Helpers
{
    public static class SpectrometerTypeDetector
    {
        public static DeviceType Detect(string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                return DeviceType.VirtualSpec;
            }

            string serial = serialNumber.Trim().ToUpper();

            if (serial.Contains("VIRTUAL") || serial.Contains("SPEC"))
            {
                return DeviceType.VirtualSpec;
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(serial, @"^\d{7}"))
            {
                return DeviceType.Avantes;
            }

            return DeviceType.Solar;
        }
    }
}
