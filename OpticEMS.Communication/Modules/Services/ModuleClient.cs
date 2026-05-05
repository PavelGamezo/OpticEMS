using Modbus.Device;
using System.Net.Sockets;

namespace OpticEMS.Communication.Modules.Services
{
    public class ModuleClient : IModuleClient
    {
        private string _ip;
        private int _port;

        private TcpClient _client;
        private ModbusIpMaster _connection;

        public ModuleClient(string ip, int port)
        {
            try
            {
                _ip = ip;
                _port = port;

                TryConnect();
            }
            catch (Exception exception)
            {
                
            }
        }

        public (int, bool) ReadInputs()
        {
            bool[] inputs = _connection.ReadInputs(1, 0, 4);

            int recipeId =
                (inputs[0] ? 1 : 0) |
                (inputs[1] ? 2 : 0) |
                (inputs[2] ? 4 : 0);

            bool start = inputs[3];

            return (recipeId, start);
        }

        public void WriteOutputs((bool b0, bool b1, bool endpoint, bool b3) state)
        {
            _connection.WriteSingleCoil(1, 16, state.b0);

            _connection.WriteSingleCoil(1, 17, state.b1);

            // ENDPOINT
            _connection.WriteSingleCoil(1, 18, state.endpoint);

            _connection.WriteSingleCoil(1, 19, state.b3);
        }

        public bool TryConnect()
        {
            try
            {
                _client = new TcpClient(_ip, _port);
                _connection = ModbusIpMaster.CreateIp(_client);

                return true;
            }
            catch 
            {
                return false;
            }
        }

        public void Dispose()
        {
            _client.Close();
            _client.Dispose();
        }

        public void SendHandshakeResponse()
        {
            _connection.WriteSingleCoil(1, 18, true);
        }
    }
}
