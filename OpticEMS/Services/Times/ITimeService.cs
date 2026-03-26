namespace OpticEMS.Services.Times
{
    public interface ITimeService
    {
        event Action<DateTime> TimeChanged;

        Task Start(CancellationToken cancellationToken);
    }
}
