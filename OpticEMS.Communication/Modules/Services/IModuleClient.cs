namespace OpticEMS.Communication.Modules.Services
{
    public interface IModuleClient : IDisposable
    {
        bool IsConnected { get; }

        /// <summary>
        /// Read 7 DIs: DI0 - DI5 is algorithm code (6 bits), DI6 is RF On
        /// Modbus FC02, offset 0, count 7.
        /// </summary>
        /// <returns></returns>
        ModuleInputState ReadInputs();

        /// <summary>
        /// Sets the End Etch signal (DO0, Modbus offset 16).
        /// True = endpoint detected / handshake response.
        /// False = reset.
        /// Modbus FC05, the only DO signal to the equipment.
        /// </summary>
        /// <param name="active"></param>
        void WriteEndEtch(bool active);

        bool TryConnect();
    }
}
