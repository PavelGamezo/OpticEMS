using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services.Calibration;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Contracts.Services.Dialog;
using OpticEMS.Contracts.Services.Etching;
using OpticEMS.Contracts.Services.Export;
using OpticEMS.Contracts.Services.Mapper;
using OpticEMS.Contracts.Services.Recipe;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Notifications.Messages;
using OpticEMS.Orchestrator;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class ChannelViewModel : ObservableObject
    {
        #region services

        private readonly IDialogService _dialogService;
        private readonly EtchingOrchestrator _orchestrator;

        #endregion

        #region observable props

        [ObservableProperty] 
        private string _processStatus = "Waiting start";

        [ObservableProperty] 
        private Recipe? _recipe;

        [ObservableProperty]
        private bool _canExport;

        #endregion

        #region viewModels

        public SpectrumChartViewModel SpectrumChartViewModel { get; }

        public ProcessChartViewModel ProcessChartViewModel { get; }

        public SpectralLinesCatalogViewModel SpectralLinesCatalogViewModel { get; }

        public ChannelDetailsViewModel ChannelDetailsViewModel { get; }

        #endregion

        #region props
        
        public int ChannelId { get; set; }

        public string ChannelName { get; private set; } = "Unknown";

        #endregion

        #region ctor

        public ChannelViewModel(int id,
            IWavelengthMapper wavelengthMapper,
            IDialogService dialogService,
            IEtchingProcessService endpointService,
            ISettingsProvider configureProvider, 
            IExportManager exportManager,
            IRecipeFileManager recipeFileManager,
            ICalibrationService calibrationService,
            ISpectralLineRepository spectralLineRepository)
        {
            _dialogService = dialogService;
            _orchestrator = new EtchingOrchestrator(
                id,
                endpointService,
                calibrationService, 
                wavelengthMapper,
                recipeFileManager, 
                configureProvider, 
                exportManager);

            ChannelId = id;
            ChannelName = $"Chamber {id + 1}";

            SpectrumChartViewModel = new SpectrumChartViewModel(
                _orchestrator.Device.Device.DeviceInfo.TrimLeft,
                _orchestrator.Device.Device.DeviceInfo.TrimRight);
            ProcessChartViewModel = new ProcessChartViewModel();
            SpectralLinesCatalogViewModel = new SpectralLinesCatalogViewModel(
                ChannelId, spectralLineRepository, dialogService);
            ChannelDetailsViewModel = new ChannelDetailsViewModel(this);

            RegisterMessages();

            SpectrumChartViewModel.OnWavelengthMoved += () =>
            {
                _orchestrator.UpdateWavelengthManually();
            };
        }

        public ChannelViewModel()
        {
            
        }

        #endregion

        #region command register

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<LiveSpectrumDataMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId == ChannelId)
                {
                    SpectrumChartViewModel.UpdateChart(message.Wavelengths, message.Intensities);
                }
            });

            
            WeakReferenceMessenger.Default.Register<DrawWindowBoundsMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId == ChannelId)
                {
                    ProcessChartViewModel.DrawWindowBounds(message.WindowBounds);
                }
            });

            WeakReferenceMessenger.Default.Register<ProcessFinishedMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId == ChannelId)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (message.IsForsed)
                        {
                            _dialogService.ShowError(message.Report);
                        }
                        else
                        {
                            _dialogService.ShowInformationWithAutoClose(message.Report);
                        }
                    });
                }
            });

            WeakReferenceMessenger.Default.Register<RecipeAppliedMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId == this.ChannelId)
                {
                    Recipe = _orchestrator.Recipe;

                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SpectrumChartViewModel.UpdateAnnotations(message.Wavelengths, message.WavelengthColors);
                    }, DispatcherPriority.Background);
                }
            });

            WeakReferenceMessenger.Default.Register<SetUpProcessChartMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId != ChannelId)
                {
                    return;
                }

                ProcessChartViewModel.SetUpModel(message.Wavelengths, message.WavelengthColors);
            });

            WeakReferenceMessenger.Default.Register<ProcessStepUpdateMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId != ChannelId)
                {
                    return;
                }

                ProcessStatus = message.Status;
                ProcessChartViewModel.UpdateTopPlot(TimeSpan.FromSeconds(message.CurrentTime), message.IntensitiesSnapshot);
                ProcessChartViewModel.StartAnnotationArea(message.Status, TimeSpan.FromSeconds(message.CurrentTime));
            });

            WeakReferenceMessenger.Default.Register<SpectralLineSelectionMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId == this.ChannelId)
                {
                    UpdateSpectrumAnnotations(message.Wavelength, (Color)ColorConverter.ConvertFromString(message.ColorHex));
                }
            });

            WeakReferenceMessenger.Default.Register<ExportAvailabilityChangedMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId == ChannelId)
                {
                    CanExport = message.CanExport;
                }
            });

            WeakReferenceMessenger.Default.Register<PcaAnomalyMapMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId == ChannelId)
                {
                    SpectrumChartViewModel.UpdateAnomaly(message.Ranges);
                }
            });
        }

        #endregion

        #region relayCommands

        [RelayCommand]
        private async Task StartProcessAsync()
        {
            try
            {
                _orchestrator.StartProcess();
            }
            catch (Exception exception)
            {
                _dialogService.ShowError(exception.Message);
            }
        }

        [RelayCommand]
        private void PauseProcess()
        {
            try
            {
                _orchestrator.PauseProcess();
            }
            catch (Exception exception)
            {
                _dialogService.ShowError(exception.Message);
            }
        }

        [RelayCommand]
        public async Task StopProcessAsync()
        {
            try
            {
                await _orchestrator.StopProcessAsync();
            }
            catch (Exception exception)
            {
                _dialogService.ShowError(exception.Message);
            }
        }

        // This will be unusefull
        [RelayCommand]
        public async Task ToggleDemoMode()
        {
            try
            {
                _orchestrator.ToggleDemoMode();
            }
            catch (Exception exception)
            {
                _dialogService.ShowError(exception.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        public void ExportToCsv()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"Channel_{ChannelId + 1}_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _orchestrator.ExportToTxt(dialog.FileName, ChannelName);
                    _dialogService.ShowInformation("Data exported successfully.");
                }
                catch (Exception exception)
                {
                    _dialogService.ShowError(exception.Message);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        public void ExportToExcel()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx",
                FileName = $"Channel_{ChannelId + 1}_Data_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _orchestrator.ExportToExcel(dialog.FileName, ChannelName);
                    _dialogService.ShowInformation("Data exported successfully.");
                }
                catch (Exception exception)
                {
                    _dialogService.ShowError(exception.Message);
                }
            }
        }


        [RelayCommand(CanExecute = nameof(CanExport))]
        public void ExportToTxt()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt",
                FileName = $"Channel_{ChannelId + 1}_Data_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _orchestrator.ExportToTxt(dialog.FileName, ChannelName);
                    _dialogService.ShowInformation("Data exported successfully.");
                }
                catch (Exception exception)
                {
                    _dialogService.ShowError(exception.Message);
                }
            }
        }

        [RelayCommand]
        private async Task ExecuteAnalizingTrain()
        {
            try
            {
                await _orchestrator.ExecuteAnalyzingTrainingAsync();
            }
            catch (Exception exception)
            {
                _dialogService.ShowError(exception.Message);
            }
        }

        #endregion

        #region methods
        
        public void ApplyRecipe(Recipe recipe)
        {
            try
            {
                _orchestrator.ApplyRecipe(recipe);
                _dialogService.ShowInformation($"Recipe '{recipe.Name}' with ID {recipe.Id} applied successfully for channel {ChannelName}");
            }
            catch (Exception exception)
            {
                _dialogService.ShowError($"Failed to apply recipe: {exception.Message}");
            }
        }

        private void UpdateSpectrumAnnotations(double wavelength, Color color)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var selectedCatalogLines = SpectralLinesCatalogViewModel.SpectralLines
                    .Where(line => line.IsSelected)
                    .ToList();

                var wavelengths = selectedCatalogLines.Select(l => l.Wavelength).ToList();
                var colors = selectedCatalogLines.Select(l => l.LineColor).ToList();

                if (Recipe != null)
                {
                    wavelengths.AddRange((IEnumerable<double>)Recipe.Wavelengths);
                    colors.AddRange((IEnumerable<Color>)Recipe.WavelengthColors);
                }

                SpectrumChartViewModel.UpdateAnnotations(wavelengths, colors);
            }, DispatcherPriority.Render);
        }

        #endregion
    }
}
