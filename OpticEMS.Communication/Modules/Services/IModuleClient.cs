namespace OpticEMS.Communication.Modules.Services
{
    public interface IModuleClient : IDisposable
    {
        (int recipeId, bool start) ReadInputs();

        void WriteOutputs((bool b0, bool b1, bool endpoint, bool b3) state);

        void SendHandshakeResponse();

        bool TryConnect();
    }
}
