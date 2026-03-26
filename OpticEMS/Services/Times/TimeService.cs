namespace OpticEMS.Services.Times
{
    public class TimeService : ITimeService
    {
        public event Action<DateTime> TimeChanged;

        public async Task Start(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested) 
            { 
                TimeChanged?.Invoke(DateTime.Now); 

                try 
                { 
                    await Task.Delay(1000, cancellationToken); 
                }
                catch (TaskCanceledException) 
                {
                    break; 
                } 
            }
        }
    }
}
