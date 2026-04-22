using OpticEMS.MVVM.Models;
using OpticEMS.MVVM.Models.Recipe;

namespace OpticEMS.Services.Etching
{
    public interface IEtchingProcessService
    {
        double DetectedAtSeconds { get; }

        double OverEtchDurationSeconds { get; }

        double TotalDurationSeconds { get; }

        void Start(RecipeModel recipe, uint[] startIntensities);

        void Pause();

        void Resume();

        void Stop();

        EndpointResult Update();

        void PushIntensities(uint[] currentIntensities);
    }
}
