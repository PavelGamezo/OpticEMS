using OpticEMS.MVVM.Models;
using OpticEMS.Notifications.Messages;
using OpticEMS.Processing.Context;
using OpticEMS.Processing.Interfaces;
using OpticEMS.Services.Etching;
using Stateless;

namespace OpticEMS.Processing
{
    public class ProcessOrchestrator : IProcessOrchestrator
    {
        private readonly StateMachine<State, Trigger> _stateMachine;

        public State CurrentState => _stateMachine.State;

        public event Action<State> StateChanged;

        private ProcessContext _processContext = new ProcessContext();

        public ProcessOrchestrator(IEtchingProcessService endpointService)
        {
            _stateMachine = new StateMachine<State, Trigger>(State.Idle);
            ConfigureMachine();
        }

        private void ConfigureMachine()
        {
            _stateMachine.Configure(State.Idle)
                .Permit(Trigger.Start, State.Stabilizing);

            _stateMachine.Configure(State.Stabilizing)
                .OnEntry(() => StartHardware())
                .Permit(Trigger.InWindowReached, State.Monitoring)
                .Permit(Trigger.Stop, State.Finished);

            _stateMachine.Configure(State.Monitoring)
                .Permit(Trigger.EndpointDetected, State.OverEtching)
                .Permit(Trigger.Stop, State.Finished);

            _stateMachine.Configure(State.OverEtching)
                .OnEntryAsync(async () => await HandleOverEtch())
                .Permit(Trigger.OverEtchFinished, State.Finished)
                .Permit(Trigger.Stop, State.Finished);

            _stateMachine.Configure(State.Finished)
            .OnEntry(() => {
                StopHardware();
                _stateMachine.Fire(Trigger.Stop);
            })
            .Permit(Trigger.Stop, State.Idle);

            _stateMachine.OnTransitioned(t => StateChanged?.Invoke(t.Destination));
        }

        private void StartHardware()
        {
        }

        private void StopHardware()
        {
        }

        public void Prepare(RecipeModel recipe) => _processContext.Recipe = recipe;

        public void Fire(Trigger trigger) => _stateMachine.Fire(trigger);

        private async Task HandleOverEtch()
        {
            var recipe = _processContext.Recipe;
            if (recipe != null && recipe.OverEtchEnabled && recipe.OverEtchValue > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(recipe.OverEtchValue));
            }

            if (_stateMachine.CanFire(Trigger.OverEtchFinished))
                _stateMachine.Fire(Trigger.OverEtchFinished);
        }

        public void EmergencyStop()
        {
            throw new NotImplementedException();
        }
    }
}
