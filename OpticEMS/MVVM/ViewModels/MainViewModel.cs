using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticEMS.MVVM.ViewModels.ProcessViewModels;
using OpticEMS.MVVM.ViewModels.RecipeViewModels;
using OpticEMS.MVVM.ViewModels.SettingsViewModels;
using OpticEMS.Services.Times;
using OpticEMS.Services.Windows;

namespace OpticEMS.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IWindowService _windowService;
        private readonly ITimeService _timeService;

        private readonly ProcessViewModel _processViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly ChamberSettingsViewModel _chamberSettingsViewModel;
        private readonly RecipeViewModel _recipeViewModel;

        private CancellationTokenSource _cancellationToken = new();

        [ObservableProperty]
        private string _currentTime;

        [ObservableProperty]
        private object _currentViewModel;

        public MainViewModel(IWindowService windowService,
            ITimeService timeService,
            ProcessViewModel process,
            SettingsViewModel settings,
            RecipeViewModel recipe,
            ChamberSettingsViewModel chamberSettingsViewModel)
        {
            _windowService = windowService;
            _timeService = timeService;

            _processViewModel = process;
            _settingsViewModel = settings;
            _recipeViewModel = recipe;

            _timeService.TimeChanged += OnTimeChanged;
            _timeService.Start(_cancellationToken.Token);

            CurrentViewModel = _processViewModel;
            _chamberSettingsViewModel = chamberSettingsViewModel;
        }

        [RelayCommand]
        private void ShowSygnal() => CurrentViewModel = _processViewModel;

        [RelayCommand]
        private void ShowSettings() => CurrentViewModel = _settingsViewModel;

        [RelayCommand]
        private void ShowChamberSettings() => CurrentViewModel = _chamberSettingsViewModel;

        [RelayCommand]
        private void ShowRecipe() => CurrentViewModel = _recipeViewModel;

        [RelayCommand]
        private void ShowTab() => CurrentViewModel = _processViewModel;

        [RelayCommand]
        private void MoveWindow() => _windowService.Move();

        [RelayCommand]
        private void CloseWindow() => _windowService.Close();

        [RelayCommand]
        private void MaximizeWindow() => _windowService.MaximizeOrRestore();

        [RelayCommand]
        public void MinimizeWindow() => _windowService.Minimize();

        private async void OnTimeChanged(DateTime time) 
        {
            CurrentTime = time.ToString("yyyy-MM-dd HH:mm:ss");
        }

        partial void OnCurrentViewModelChanged(object value)
        {
            OnPropertyChanged(nameof(IsProcessSelected));
            OnPropertyChanged(nameof(IsRecipeSelected));
            OnPropertyChanged(nameof(IsSettingsSelected));
        }

        public bool IsProcessSelected => CurrentViewModel is ProcessViewModel;
        public bool IsRecipeSelected => CurrentViewModel is RecipeViewModel;
        public bool IsSettingsSelected => CurrentViewModel is SettingsViewModel;

        public void Dispose()
        {
            _cancellationToken?.Cancel();
            _cancellationToken?.Dispose();
        }
    }
}
