using Modbus.Device;
using OpticEMS.Communication.Modules.Services;
using Serilog;
using System.Data.Common;
using System.Diagnostics;
using System.Net.Sockets;

namespace OpticEMS.Communication.Modules
{
    public class ModuleHandler : IDisposable
    {
        private const int POLL_INTERVAL_MS = 50;

        private (int recipeId, bool start) _prevState = (-1, false);

        private IModuleClient _client;
        private CancellationTokenSource _cancellationToken = new();

        public event Action<(int, bool)> OnInputChanged;

        public ModuleHandler(string ip, int port)
        {
            _client = new ModuleClient(ip, port);

            Task.Run(() => PollLoop());
        }

        private async Task PollLoop()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var state = _client.ReadInputs();

                    if (HasStateChanged(state))
                    {
                        OnInputChanged?.Invoke(state);
                    }
                }
                catch (Exception exception)
                {
                    Log.Error($"Modbus read failed: {exception.Message}");
                    Log.Warning("Attempting reconnect...");

                    while (!_client.TryConnect())
                    {
                        Log.Warning("Reconnect failed, retrying...");
                        await Task.Delay(1000);
                    }

                    Log.Information("Reconnected successfully.");
                }

                await Task.Delay(POLL_INTERVAL_MS);
            }
        }

        private bool HasStateChanged((int recipeId, bool start) current)
        {
            var hasStateChanged = current.recipeId != _prevState.recipeId ||
                   current.start != _prevState.start;

            _prevState = current;

            return hasStateChanged;
        }


        public void SetOutputs((bool b0, bool b1, bool endpoint, bool b3) state)
        {
            try
            {
                _client.WriteOutputs(state);
            }
            catch (Exception exception)
            {
                // Logging
            }
        }

        public void Stop() => _cancellationToken.Cancel();

        public void Dispose()
        {
            _cancellationToken.Cancel();
            _client.Dispose();
        }
    }
}
