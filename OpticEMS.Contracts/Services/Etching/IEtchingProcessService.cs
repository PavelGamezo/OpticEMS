using OpticEMS.Services.Etching;

namespace OpticEMS.Contracts.Services.Etching
{
    public interface IEtchingProcessService
    {
        double DetectedAtSeconds { get; }

        double OverEtchDurationSeconds { get; }

        double TotalDurationSeconds { get; }

        void Start(Recipe.Recipe recipe, double[] startIntensities);

        List<WindowBounds> GetConfirmedWindowsIn();

        List<WindowBounds> GetConfirmedWindowsOut();

        void ClearConfirmedWindowsIn();

        void ClearConfirmedWindowsOut();

        List<WindowBounds> GetCurrentWindowBounds();

        void Stop();

        EndpointResult Update(double[] signal, double elapsedMs);
    }
}
