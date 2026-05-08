using OpticEMS.Contracts.Services.Settings;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace OpticEMS.Devices.Devices.Yixis
{
    internal class Yixist : Device
    {
        private readonly SerialPort _port;
        private readonly object _lock = new object();
        private bool _isInitialized;

        public Yixist(string portName, int boundRate)
        {
            _port = new SerialPort(portName, boundRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 2000,
                WriteTimeout = 1000
            };

            DeviceInfo = new DeviceInfo("YIXIST", 2048, 0, 0, DeviceType.Yixist, 0, 0, 0, 0, 0, 0, 0);
        }

        public override DeviceInfo DeviceInfo { get; set; }

        public override void Initialize(int id)
        {
            throw new NotImplementedException();
        }

        public override bool Scan(int id, double[] collection, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override void SetParameters(int id, float exposureMs, int scansNum)
        {
            throw new NotImplementedException();
        }

        public override void StopMeasurement()
        {
            throw new NotImplementedException();
        }
    }
}
