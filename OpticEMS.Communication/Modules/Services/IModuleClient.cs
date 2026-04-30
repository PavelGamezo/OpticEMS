namespace OpticEMS.Communication.Modules.Services
{
    public interface IModuleClient : IDisposable
    {
        (int, bool) ReadInputs();

        void WriteOutputs((bool b0, bool b1, bool endpoint, bool b3) state);

        bool TryConnect();
    }
}
