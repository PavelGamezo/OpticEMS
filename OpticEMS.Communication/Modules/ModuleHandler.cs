using OpticEMS.Communication.Modules.Services;

namespace OpticEMS.Communication.Modules
{
    public class ModuleHandler
    {
        private readonly IModuleClient _client;
        private readonly CancellationTokenSource _cancellationToken = new();

        public ModuleHandler(IModuleClient client)
        {
            _client = client;

            _ = PollLoop();
        }

        private async Task PollLoop()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                var state = _client.ReadInputs();
                //OnInputChanged?.Invoke(state);
                //Send message

                await Task.Delay(50);
            }
        }

        public void SetOutputs((bool, bool, bool, bool) state)
        {
            _client.WriteOutputs(state);
        }

        public void Stop() => _cancellationToken.Cancel();

    }
}
