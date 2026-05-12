namespace OpticEMS.Contracts.Services.Etching
{
    public enum EndpointStatus
    {
        Idle,
        NoRecipe,
        SignalWaiting,
        Timeout,
        InitialDeadTime,
        Monitoring,
        Endpoint,
        Overetching,
        Completed
    }
}
