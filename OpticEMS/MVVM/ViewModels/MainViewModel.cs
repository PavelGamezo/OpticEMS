using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticEMS.Contracts.Services.Dialog;
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
        private readonly IDialogService _dialogService;

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
            IDialogService dialogService,
            ITimeService timeService,
            ProcessViewModel process,
            SettingsViewModel settings,
            RecipeViewModel recipe,
            ChamberSettingsViewModel chamberSettingsViewModel)
        {
            _windowService = windowService;
            _timeService = timeService;
            _dialogService = dialogService;

            _processViewModel = process;
            _settingsViewModel = settings;
            _recipeViewModel = recipe;
            _chamberSettingsViewModel = chamberSettingsViewModel;

            try
            {
                _timeService.TimeChanged += OnTimeChanged;
                _timeService.Start(_cancellationToken.Token);

                CurrentViewModel = _processViewModel;

                Serilog.Log.Information("[MainViewModel]: Successfully initialized and started.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "[MainViewModel]: Critical failure during startup");
            }
        }

        [RelayCommand]
        private void ShowSygnal()
        {
            Serilog.Log.Debug("[MainViewModel]: Switching to Process View");
            CurrentViewModel = _processViewModel;
        }

        [RelayCommand]
        private void ShowSettings()
        {
            Serilog.Log.Information("[MainViewModel]: Requesting access to Settings (Password required)");
            if (_dialogService.AskPassword())
            {
                Serilog.Log.Information("[MainViewModel]: Password accepted. Accessing Settings View");
                CurrentViewModel = _settingsViewModel;
            }
            else
            {
                Serilog.Log.Warning("[MainViewModel]: Access to Settings denied (Incorrect password or cancelled)");
            }
        }

        [RelayCommand]
        private void ShowChamberSettings()
        {
            Serilog.Log.Information("[MainViewModel]: Switching to ChamberSettingsView");
            CurrentViewModel = _chamberSettingsViewModel;
        }

        [RelayCommand]
        private void ShowRecipe()
        {
            Serilog.Log.Information("[MainViewModel]: Switching to RecipeView");
            CurrentViewModel = _recipeViewModel;
        }

        [RelayCommand]
        private void MoveWindow() => _windowService.Move();

        [RelayCommand]
        private void CloseWindow()
        {
            Serilog.Log.Information("[MainViewModel]: User requested to close the application window");
            _windowService.Close();
        }

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
            Serilog.Log.Debug("[MainViewModel]: Current View changed to {ViewModelType}", value?.GetType().Name);
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
