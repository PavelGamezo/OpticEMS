using OpticEMS.MVVM.Models;

namespace OpticEMS.Services.Etching
{
    public interface IEtchingProcessService
    {
        void Start(RecipeModel recipe, uint[] startIntensities);

        EndpointResult CheckEndpoint(uint[] currentIntensities, double elapsedMs);

        void Stop();
    }
}
