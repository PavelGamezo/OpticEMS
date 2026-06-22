namespace OpticEMS.Contracts.Services
{
    public sealed record ReprocessResult(
        bool EndpointFound,
        double EndpointSeconds,
        double TotalSeconds,
        string Report);
}
