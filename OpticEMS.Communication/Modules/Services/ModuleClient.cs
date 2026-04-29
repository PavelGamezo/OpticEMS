using Modbus.Device;
using System.Net.Sockets;

namespace OpticEMS.Communication.Modules.Services
{
    public class ModuleClient : IModuleClient, IDisposable
    {
        private string _ip;
        private int _port;

        private readonly TcpClient _client;
        private readonly ModbusIpMaster _connection;

        public ModuleClient(string ip, int port)
        {
            _client = new TcpClient(ip, port);
            _connection = ModbusIpMaster.CreateIp(_client);
        }

        public (int, bool) ReadInputs()
        {
            bool[] inputs = _connection.ReadInputs(0, 4);

            int recipeId =
                (inputs[0] ? 1 : 0) |
                (inputs[1] ? 2 : 0) |
                (inputs[2] ? 4 : 0);

            bool start = inputs[3];

            return (recipeId, start);
        }

        public void WriteOutputs((bool, bool, bool, bool) state)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
