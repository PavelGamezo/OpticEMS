using System.IO.Ports;
using System.Text;

namespace OpticEMS.Devices.Devices.Yixis
{
    public static class YixistCCD
    {
        public const byte CMD_READ_SP_INFO = 0x01;
        public const byte CMD_SET_SPECTRUM_PARAS = 0x03;
        public const byte CMD_READ_SPECTRUM_DATA = 0x04;
        public const byte CMD_CAL_DATA = 0x07;
        public const byte CMD_SET_SYN_PULSE_OUTPUT = 0x0A;
        public const byte CMD_READ_NL_DATA = 0x0B;

        public const byte HEADER = 0xAA;
        public const byte ORIGIN_PC = 0x00;
        public const byte ORIGIN_DEVICE = 0x01;
        public const byte END = 0x7E;

        /// <summary>
        /// Creates packet request for spectrometer 
        /// </summary>
        /// <param name="command"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static byte[] BuildRequest(byte command, byte[]? buffer = null)
        {
            buffer ??= Array.Empty<byte>();

            int length = 1 + 1 + 2 + 1 + buffer.Length + 2 + 1;

            byte[] packet = new byte[length + 5];
            var idx = 0;

            packet[idx++] = HEADER;
            packet[idx++] = ORIGIN_PC;
            packet[idx++] = (byte)(length >> 8);
            packet[idx++] = (byte)length;
            packet[idx++] = command;

            if (buffer.Length > 0)
            {
                Array.Copy(buffer, 0, packet, idx, buffer.Length);
                idx += buffer.Length;
            }

            packet[idx++] = 0x00;
            packet[idx++] = 0x00;

            packet[idx++] = END;

            for (int i = 0; i < 5; i++)
            {
                packet[idx++] = 0x55;
            }

            return packet;
        }


        public static byte[] SendCommand(SerialPort port, byte command, byte[]? data = null)
        {
            byte[] request = BuildRequest(command, data);
            port.Write(request, 0, request.Length);

            int bytesToRead = port.BytesToRead;
            byte[] response = new byte[bytesToRead];
            port.Read(response, 0, bytesToRead);

            return response;
        }

        /// <summary>
        /// Set the exposure time, scans numbers to averages, and the working mode
        /// </summary>
        /// <param name="port"></param>
        /// <param name="exposureMs"></param>
        /// <param name="averages"></param>
        /// <param name="mode"></param>
        public static void SetSpectrumParams(SerialPort port, float exposureMs, int averages = 1, byte mode = 0x11)
        {
            int exposureUs = (int)(exposureMs * 1000);

            byte[] data = new byte[5];
            data[0] = (byte)((exposureUs >> 16) & 0xFF);
            data[1] = (byte)((exposureUs >> 8) & 0xFF);
            data[2] = (byte)(exposureUs & 0xFF);
            data[3] = (byte)averages;
            data[4] = mode;

            SendCommand(port, CMD_SET_SPECTRUM_PARAS, data);
        }

        /// <summary>
        /// Read the spectral data
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public static byte[] ReadSpectrumData(SerialPort port)
        {
            var packet = SendCommand(port, CMD_READ_SPECTRUM_DATA);

            return packet;
        }

        /// <summary>
        /// Read the basic information of the spectrometer, including Model, SN, Detector,
        /// Resolution, Wavelength range, Slit, and Firmware Version. Each message is separated 
        /// by a newline character
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public static string ReadDeviceInfo(SerialPort port)
        {
            byte[] response = SendCommand(port, CMD_READ_SP_INFO);
            return Encoding.ASCII.GetString(response).Trim('\0', '\r', '\n');
        }
    }
}
