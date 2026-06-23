namespace OpticEMS.Contracts.Services.Import
{
    public sealed record TraceSeries(
        string Name,
        IReadOnlyList<(double TimeSeconds, double Intensity)> Points);
}
