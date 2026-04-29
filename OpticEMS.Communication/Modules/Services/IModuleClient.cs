namespace OpticEMS.Communication.Modules.Services
{
    public interface IModuleClient
    {
        (int, bool) ReadInputs();

        void WriteOutputs((bool, bool, bool, bool) state);
    }
}
