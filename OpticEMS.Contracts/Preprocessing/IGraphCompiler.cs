namespace OpticEMS.Contracts.Preprocessing
{
    public interface IGraphCompiler
    {
        List<ExecutionStep> Compile(string graphJson);
    }
}
