using Modbus.Device;
using Serilog;
using System.Net.Sockets;

namespace OpticEMS.Communication.Modules.Services
{
    public class ModuleClient : IModuleClient
    {
        private const ushort DiStartOffset = 0;
        private const ushort DiCount = 7;
        private const ushort EndEtchDoOffset = 16;

        private string _ip;
        private int _port;
        private byte _unitId;

        private TcpClient _client;
        private ModbusIpMaster _master;

        public ModuleClient(string ip, int port)
        {
            _ip = ip;
            _port = port;
        }

        public bool IsConnected => _client?.Connected == true;

        public bool TryConnect()
        {
            try
            {
                _client?.Dispose();
                _client = new TcpClient(_ip, _port);
                _master = ModbusIpMaster.CreateIp(_client);

                Log.Information("[MODULE_CLIENT]: Connected to {Ip} (Unit={Unit})", _ip, _unitId);
                return true;
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "[MODULE_CLIENT]: Connect failed to {Ip}", _ip);
                return false;
            }
        }

        ModuleInputState IModuleClient.ReadInputs()
        {
            EnsureConnected();

            bool[] di = _master!.ReadInputs(_unitId, DiStartOffset, DiCount);

            int code = 0;
            for (int i = 0; i < 6; i++)
            {
                if (di[i]) code |= (1 << i);
            }
            bool rfOn = di[6];

            return new ModuleInputState(code, rfOn);
        }

        public void WriteEndEtch(bool active)
        {
            EnsureConnected();

            _master!.WriteSingleCoil(_unitId, EndEtchDoOffset, active);
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException($"[MODULE_CLIENT]: Not connected to {_ip}");
            }
        }

        public void Dispose()
        {
            _master = null;
            _client?.Close();
            _client?.Dispose();
        }
    }
}
