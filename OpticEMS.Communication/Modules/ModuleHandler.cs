using OpticEMS.Communication.Modules.Services;
using Serilog;

namespace OpticEMS.Communication.Modules
{
    /// <summary>
    /// Implements the Endpoint protocol:
    /// 1. IDLE: AlgorithmCode = 0, RfOn = false → wait
    /// 2. HANDSHAKE: AlgorithmCode = 63, any RfOn → reply with End Etch pulse
    /// 3. PROCESS: AlgorithmCode = 1-62, RfOn = true → run recipe
    /// 4. ENDPOINT: process completed → set End Etch
    /// 5. RESET: AlgorithmCode = 0 → remove End Etch → return to IDLE
    /// </summary>
    public class ModuleHandler : IDisposable
    {
        private const int POLL_INTERVAL_MS = 50;
        private const int RECONNECT_INTERVAL_MS = 5000;
        private const int HANDSHAKE_CODE = 63;

        private IModuleClient _client;
        private CancellationTokenSource _cancellationToken = new();

        private ModuleInputState _prevState = new(0, false);
        private bool _isReady = true;
        private bool _disposed;

        /// <summary>
        /// Centura requests a recipe to run.
        /// Parameter — AlgorithmCode (1-62).
        /// Called when the AlgorithmCode is valid AND RfOn=true.
        /// </summary>
        public event Action<int>? ProcessStartRequested;

        public ModuleHandler(string ip, int port)
        {
            _client = new ModuleClient(ip, port);
            Task.Run(() => RunAsync(_cancellationToken.Token));
        }

        /// <summary>
        /// Endpoint detected - set End Etch.
        /// Equipment will detect the signal, turn off RF, and reset AlgorithmCode to 0.
        /// </summary>
        public void SendEndEtch()
        {
            _isReady = false;
            SafeWriteEndEtch(true);
            Log.Information("[MODULE_HANDLER]: End Etch signal sent");
        }

        /// <summary>
        /// Reset End Etch after Centura has confirmed (AlgorithmCode=0).
        /// Called automatically from the poll loop, but can also be called manually.
        /// </summary>
        public void Reset()
        {
            SafeWriteEndEtch(false);
            _isReady = true;
            Log.Information("[MODULE_HANDLER]: Reset — ready for next process");
        }

        private async Task RunAsync(CancellationToken ct)
        {
            Log.Information("[MODULE_HANDLER]: Starting");
            await EnsureConnectedAsync(ct);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var state = _client.ReadInputs();

                    if (state != _prevState)
                    {
                        await HandleStateChangeAsync(state, ct);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[MODULE_HANDLER]: Read error, reconnecting...");
                    await EnsureConnectedAsync(ct);
                }

                await Task.Delay(POLL_INTERVAL_MS, ct).ConfigureAwait(false);
            }
        }

        private async Task HandleStateChangeAsync(ModuleInputState state, CancellationToken ct)
        {
            var prev = _prevState;
            _prevState = state;

            Log.Debug("[MODULE_HANDLER]: State → Code={Code}, RfOn={Rf}",
                state.AlgorithmCode, state.RfOn);

            switch (state.AlgorithmCode)
            {
                case 0:
                    await HandleIdleAsync(prev);
                    break;

                case HANDSHAKE_CODE:
                    await HandleHandshakeAsync(prev, ct);
                    break;

                default:
                    HandleProcessCode(state);
                    break;
            }
        }

        /// <summary>
        /// Code=0: Centura has reset the bits.
        /// If End Etch was previously set, clear it (Centura has confirmed).
        /// </summary>
        private Task HandleIdleAsync(ModuleInputState prev)
        {
            if (prev.AlgorithmCode != 0)
            {
                Log.Information("[MODULE_HANDLER]: Code cleared by Centura → resetting End Etch");
                Reset();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Code=63: Handshake request from Centura.
        ///
        /// 1. The previous code was 0 (stable low → toggled to high)
        /// 2. The controller is in the IsReady state
        ///
        /// Response: Set End Etch, wait for Equipment to reset the code, then remove End Etch.
        /// </summary>
        private async Task HandleHandshakeAsync(ModuleInputState prev, CancellationToken ct)
        {
            if (prev.AlgorithmCode != 0)
            {
                Log.Warning("[MODULE_HANDLER]: Handshake ignored — previous code was {Code}, not 0",
                    prev.AlgorithmCode);
                return;
            }

            if (!_isReady)
            {
                Log.Warning("[MODULE_HANDLER]: Handshake ignored — not in ready state (process running)");
                return;
            }

            Log.Information("[MODULE_HANDLER]: Handshake received — sending End Etch response");

            SafeWriteEndEtch(true);

            var deadline = Environment.TickCount64 + 3000;

            while (Environment.TickCount64 < deadline && !ct.IsCancellationRequested)
            {
                await Task.Delay(POLL_INTERVAL_MS, ct);

                try
                {
                    var current = _client.ReadInputs();
                    _prevState = current;

                    if (current.AlgorithmCode == 0)
                    {
                        Log.Information("[MODULE_HANDLER]: Handshake acknowledged by Centura");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[MODULE_HANDLER]: Read error during handshake wait");
                    break;
                }
            }

            SafeWriteEndEtch(false);
        }

        /// <summary>
        /// Code=1-62: Centura has set the recipe.
        /// Start the process only if RF is On (Centura has actually started the process).
        /// </summary>
        private void HandleProcessCode(ModuleInputState state)
        {
            if (!_isReady)
            {
                return;
            }

            if (!state.RfOn)
            {
                Log.Debug("[MODULE_HANDLER]: Code={Code} received but RF not on yet",
                    state.AlgorithmCode);

                return;
            }

            Log.Information("[MODULE_HANDLER]: Process start: Code={Code}, RfOn=true",
                state.AlgorithmCode);

            _isReady = false;
            ProcessStartRequested?.Invoke(state.AlgorithmCode);
        }

        private async Task EnsureConnectedAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && !_client.TryConnect())
            {
                Log.Warning("[MODULE_HANDLER]: Reconnect failed, retry in {Ms}ms",
                    RECONNECT_INTERVAL_MS);
                await Task.Delay(RECONNECT_INTERVAL_MS, ct).ConfigureAwait(false);
            }
        }

        private void SafeWriteEndEtch(bool active)
        {
            try
            {
                _client.WriteEndEtch(active);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MODULE_HANDLER]: WriteEndEtch({Active}) failed", active);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _cancellationToken.Cancel();
            _cancellationToken.Dispose();
            _client.Dispose();
        }
    }
}
