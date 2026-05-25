namespace OpticEMS.Contracts.Preprocessing
{
    public interface INodeProcessor
    {
        double Process(double[] inputs, double currentTimeMs);

        void Reset();
    }
}
