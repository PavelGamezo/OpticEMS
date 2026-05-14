using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticEMS.Orchestrator;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class SpectrometerControlViewModel : ObservableObject
    {
        private readonly EtchingOrchestrator _orchestrator;

        [ObservableProperty]
        private float _exposureMs = 1;

        [ObservableProperty]
        private int _scansNum = 1;

        public SpectrometerControlViewModel(
            EtchingOrchestrator orchestrator,
            float exposureMs,
            int scanNums)
        {
            _orchestrator = orchestrator;

            ExposureMs = exposureMs;
            ScansNum = scanNums;
        }

        [RelayCommand]
        public void ApplySpecParams()
        {
            _orchestrator.ApplySpecParams(ExposureMs, ScansNum);
        }
    }
}
