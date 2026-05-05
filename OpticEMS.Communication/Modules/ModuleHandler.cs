using OpticEMS.Communication.Modules.Services;
using Serilog;

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

                    if (state != _prevState)
                    {
                        HandleInputState(state);
                    }
                }
                catch (Exception exception)
                {
                    Log.Error($"Modbus read failed: {exception.Message}");
                    Log.Warning("Attempting reconnect...");

                    while (!_client.TryConnect())
                    {
                        Log.Warning("Reconnect failed, retrying...");
                        await Task.Delay(5000);
                    }

                    Log.Information("Reconnected successfully.");
                }

                await Task.Delay(POLL_INTERVAL_MS);
            }
        }

        private void HandleInputState((int recipeId, bool start) state)
        {
            if (state.recipeId == 7)
            {
                _client.SendHandshakeResponse();
                Log.Information("Handshake request received. Responding ready");
                
                return;
            }

            if (state.recipeId > 0 && state.recipeId <= 6)
            {
                OnInputChanged?.Invoke(state);
            }

            _prevState = state;
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
