using OpticEMS.Contracts.Preprocessing;
using System.Text.Json;

namespace OpticEMS.Contracts.Factories
{
    public interface INodeProcessorFactory
    {
        INodeProcessor CreateProcessor(string nodeType, JsonElement jsonProperties);
    }
}
