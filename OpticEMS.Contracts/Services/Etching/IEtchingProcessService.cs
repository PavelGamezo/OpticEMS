using OpticEMS.Contracts.Services.Recipe;

namespace OpticEMS.Contracts.Services.Etching
{
    public interface IEtchingProcessService
    {
        double DetectedAtSeconds { get; }

        double OverEtchDurationSeconds { get; }

        double TotalDurationSeconds { get; }

        void Start(Recipe.Recipe recipe, uint[] startIntensities);

        void Pause();

        void Resume();

        void Stop();

        EndpointResult Update();

        void PushIntensities(uint[] currentIntensities);
    }
}
