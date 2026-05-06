namespace OpticEMS.Contracts.Services.SignalPreprocessing
{
    public interface ISignalOperation
    {
        uint ComputeAvg(uint value);

        double ComputeDer(uint value);

        uint[] ComputeAvg(uint[] values, double elapsedMs);

        double[] ComputeDer(uint[] values, double elapsedMs);

        void Reset();

        string Name { get; }

        string Description { get; }
    }
}
