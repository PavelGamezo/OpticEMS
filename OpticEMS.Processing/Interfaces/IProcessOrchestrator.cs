using OpticEMS.Notifications.Messages;

namespace OpticEMS.Processing.Interfaces
{
    public interface IProcessOrchestrator
    {
        State CurrentState { get; }
        
        event Action<State> StateChanged;

        void Fire(Trigger trigger);

        void EmergencyStop();
    }
}
