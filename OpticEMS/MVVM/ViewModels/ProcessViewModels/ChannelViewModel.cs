using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using OpticEMS.Common.Helpers.OxyPlot;
using OpticEMS.Contracts.Services.Calibration;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Contracts.Services.Dialog;
using OpticEMS.Contracts.Services.Etching;
using OpticEMS.Contracts.Services.Mapper;
using OpticEMS.Contracts.Services.Recipe;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Notifications.Messages;
using OpticEMS.Notifications.Messages.SpectralLines;
using OpticEMS.Orchestrator;
using OpticEMS.Processing.SpectrumScanner;
using OpticEMS.Services.Import;
using Serilog;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class ChannelViewModel : ObservableObject, IDisposable
    {
        private bool _isDisposed;

        #region services

        private readonly IDialogService _dialogService;
        private readonly EtchingOrchestrator _orchestrator;
        private readonly ReprocessOrchestrator _reprocessOrchestrator;
        private readonly SpectrumScanner _spectrumScanner;

        #endregion

        #region observable props

        [ObservableProperty]
        private string _processStatus = "Waiting start";

        [ObservableProperty]
        private Recipe? _recipe;

        [ObservableProperty]
        private bool _canExport;

        [ObservableProperty]
        private bool _isNoiseFloorMode;

        #endregion

        #region viewModels

        public SpectrumChartViewModel SpectrumChartViewModel { get; }

        public ProcessChartViewModel ProcessChartViewModel { get; }

        public SpectralLinesCatalogViewModel SpectralLinesCatalogViewModel { get; }

        public ChannelDetailsViewModel ChannelDetailsViewModel { get; }

        public SpectrometerControlViewModel SpectrometerControlViewModel { get; }

        public SpectrumScanChartViewModel SpectrumScanChartViewModel { get; }

        #endregion

        #region props

        public int ChannelId { get; set; }

        public string ChannelName { get; private set; } = "Unknown";

        #endregion

        #region ctor

        public ChannelViewModel(int id,
            IWavelengthMapper wavelengthMapper,
            IRecipeRepository recipeRepository,
            IDialogService dialogService,
            IEtchingProcessService endpointService,
            ISettingsProvider configureProvider,
            ICalibrationService calibrationService,
            ISpectralLineRepository spectralLineRepository)
        {
            Log.Information("[ChannelViewModel #{Id}]: Creating channel '{Name}'", id, $"Chamber {id + 1}");

            _dialogService = dialogService;
            _orchestrator = new EtchingOrchestrator(
                id, endpointService, recipeRepository,
                calibrationService, wavelengthMapper, configureProvider);
            _reprocessOrchestrator = new ReprocessOrchestrator(id, endpointService);

            _spectrumScanner = new SpectrumScanner(); 
            _spectrumScanner.ResultReady += OnNoiseFloorResult;

            ChannelId = id;
            ChannelName = $"Chamber {id + 1}";

            SpectrumChartViewModel = new SpectrumChartViewModel(
                _orchestrator.Device.Device.DeviceInfo.TrimLeft,
                _orchestrator.Device.Device.DeviceInfo.TrimRight);
            ProcessChartViewModel = new ProcessChartViewModel();
            SpectralLinesCatalogViewModel = new SpectralLinesCatalogViewModel(
                ChannelId, spectralLineRepository, dialogService);
            ChannelDetailsViewModel = new ChannelDetailsViewModel(this);
            SpectrometerControlViewModel = new SpectrometerControlViewModel(
                ChannelId, _orchestrator, configureProvider);
            SpectrumScanChartViewModel = new SpectrumScanChartViewModel(
                _orchestrator.Device.Device.DeviceInfo.TrimLeft,
                _orchestrator.Device.Device.DeviceInfo.TrimRight);
            
            RegisterMessages();

            SpectrumChartViewModel.OnWavelengthMoved += (index, wavelength) =>
            {
                _orchestrator.UpdateWavelengthManually(index, wavelength);
            };

            Log.Information("[ChannelViewModel #{Id}]: Channel created successfully", id);
        }

        public ChannelViewModel()
        {

        }

        #endregion

        #region messages register

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<LiveSpectrumDataMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId == ChannelId)
                {
                    if (Application.Current?.Dispatcher.HasShutdownStarted == true || _isDisposed)
                    {
                        return;
                    }

                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        SpectrumChartViewModel.UpdateChart(message.Wavelengths, message.Intensities);
                    }, DispatcherPriority.Render);
                }
            });


            WeakReferenceMessenger.Default.Register<DrawWindowBoundsMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId == ChannelId)
                {
                    if (Application.Current?.Dispatcher.HasShutdownStarted == true || _isDisposed)
                    {
                        return;
                    }

                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        ProcessChartViewModel.DrawWindowBounds(
                            message.WindowBounds,
                            message.ConfirmedWindowsIn,
                            message.ConfirmedWindowsOut);
                    }, DispatcherPriority.Render);
                }
            });

            WeakReferenceMessenger.Default.Register<ProcessFinishedMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId == ChannelId)
                {
                    if (Application.Current?.Dispatcher.HasShutdownStarted == true || _isDisposed)
                    {
                        return;
                    }

                    Application.Current.Dispatcher.InvokeAsync(() =>
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

                    if (Application.Current?.Dispatcher.HasShutdownStarted == true || _isDisposed)
                    {
                        return;
                    }

                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            UpdateSpectrumAnnotations();
                        }
                        catch (Exception exception)
                        {
                            Log.Error(exception, "[ORCHESTRATOR]: Error during async annotations update after recipe applying");
                        }
                    }, DispatcherPriority.Render);
                }
            });

            WeakReferenceMessenger.Default.Register<SetUpProcessChartMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId != ChannelId)
                {
                    return;
                }

                if (Application.Current?.Dispatcher.HasShutdownStarted == true || _isDisposed)
                {
                    return;
                }

                Application.Current?.Dispatcher.InvokeAsync(() => 
                {
                    ProcessChartViewModel.SetUpModel(message.Wavelengths, message.WavelengthColors); 
                }, DispatcherPriority.Render);
            });

            WeakReferenceMessenger.Default.Register<ProcessStepUpdateMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId != ChannelId)
                {
                    return;
                }

                if (Application.Current?.Dispatcher.HasShutdownStarted == true || _isDisposed)
                {
                    return;
                }

                ProcessStatus = message.Status;

                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        ProcessChartViewModel.UpdateTopPlot(TimeSpan.FromSeconds(message.CurrentTime), message.IntensitiesSnapshot);
                        ProcessChartViewModel.StartAnnotationArea(message.Status, TimeSpan.FromSeconds(message.CurrentTime));
                    }
                    catch (Exception exception)
                    {
                        Log.Error(exception, "[ORCHESTRATOR]: Error during async process step update");
                    }
                });
            });

            WeakReferenceMessenger.Default.Register<SpectralLineSelectionMessage>(this, (recipient, message) =>
            {
                if (message.ChannelId == this.ChannelId)
                {
                    if (Application.Current?.Dispatcher.HasShutdownStarted == true || _isDisposed)
                    {
                        return;
                    }

                    Application.Current?.Dispatcher.InvokeAsync(() => 
                    {
                        try
                        {
                            UpdateSpectrumAnnotations();
                        }
                        catch (Exception exception)
                        {
                            Log.Error(exception, "[ORCHESTRATOR]: Error during async annotations update");
                        }
                    }, DispatcherPriority.Render);
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
                    if (Application.Current?.Dispatcher.HasShutdownStarted == true)
                    {
                        return;
                    }

                    Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        SpectrumChartViewModel.UpdateAnomaly(message.Ranges); 
                    }, DispatcherPriority.Render);
                }
            });

            WeakReferenceMessenger.Default.Register<ApplicationShutdownMessage>(this, (recipient, message) =>
            {
                Dispose();
            });
        }

        #endregion

        #region relayCommands

        [RelayCommand]
        private async Task StartProcessAsync()
        {
            try
            {
                if (Recipe != null)
                {
                    if (_spectrumScanner.IsScanning)
                    {
                        Log.Debug("[ChannelViewModel #{Id}]: Stopping spectrum scanner before process start", ChannelId);
                        _spectrumScanner.Stop();
                    }

                    IsNoiseFloorMode = false;

                    Log.Information("[ChannelViewModel #{Id}]: Starting etching process. Recipe='{Recipe}'",
                        ChannelId, Recipe.Name);

                    _orchestrator.StartProcess();
                }
                else
                {
                    Log.Information("[ChannelViewModel #{Id}]: No recipe applied — starting scanning mode",
                        ChannelId);

                    IsNoiseFloorMode = true;
                    _spectrumScanner.Start(ChannelId, sigmaMultiplier: 3.0);
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[ChannelViewModel #{Id}]: Failed to start process", ChannelId);
                _dialogService.ShowError(exception.Message);
            }
        }

        [RelayCommand]
        public async Task StopProcessAsync()
        {
            try
            {
                if (_spectrumScanner.IsScanning)
                {
                    Log.Information("[ChannelViewModel #{Id}]: Stopping noise floor scan", ChannelId);
                    _spectrumScanner.Stop();
                    IsNoiseFloorMode = false;
                    return;
                }

                Log.Information("[ChannelViewModel #{Id}]: Stopping etching process", ChannelId);
                await _orchestrator.StopProcessAsync();
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[ChannelViewModel #{Id}]: Failed to stop process", ChannelId);
                _dialogService.ShowError(exception.Message);
            }
        }

        [RelayCommand]
        public void ExportData()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx|CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt",
                DefaultExt = ".xlsx",
                FileName = $"Channel_{ChannelId + 1}_Data_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                string extension = System.IO.Path.GetExtension(dialog.FileName).ToLower();
                OxyExportType exportType = extension switch
                {
                    ".xlsx" => OxyExportType.Excel,
                    ".csv" => OxyExportType.CommaSeparatedValues,
                    ".txt" => OxyExportType.Text,
                    _ => OxyExportType.Text
                };

                var exportData = _orchestrator.GetExportData();

                ProcessChartViewModel.Export(
                    type: exportType,
                    startTime: exportData.StartTime,
                    endTime: exportData.EndTime,
                    overEtchStartTime: exportData.OverEtchStartTime,
                    overEtchEndTime: exportData.OverEtchEndTime,
                    recipeName: Recipe.Name,
                    channelName: ChannelName,
                    path: dialog.FileName
                );

                Log.Information("[ChannelViewModel #{Id}]: Data exported to '{Path}'", ChannelId, dialog.FileName);

                _dialogService.ShowInformation("Data exported successfully.");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[ChannelViewModel #{Id}]: Export failed", ChannelId);
                _dialogService.ShowError($"Export failed: {exception.Message}");
            }
        }

        [RelayCommand]
        private async Task StartReprocessAsync()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Select trace file for reprocess",
                    Filter = "Trace files (*.csv;*.txt)|*.csv;*.txt|All files (*.*)|*.*"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                Log.Information("[ChannelViewModel #{Id}]: Starting reprocess from '{File}'",
                    ChannelId, dialog.FileName);

                var importData = TraceCsvParser.Parse(dialog.FileName);

                Log.Information("[ChannelViewModel #{Id}]: Trace parsed. Duration={Dur}s, Series={Count}",
                    ChannelId, importData.DurationSeconds, importData.Series.Count);

                _reprocessOrchestrator.Stop();
                _reprocessOrchestrator.Start(importData, Recipe);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[ChannelViewModel #{Id}]: Reprocess failed", ChannelId);
                _dialogService.ShowError(exception.Message);
            }
        }

        [RelayCommand]
        private void StopReprocess()
        {
            _reprocessOrchestrator.Stop();

            StartReprocessCommand.NotifyCanExecuteChanged();
            StopReprocessCommand.NotifyCanExecuteChanged();
        }

        #endregion

        #region methods

        public void ApplyRecipe(Recipe recipe)
        {
            try
            {
                _orchestrator.ApplyRecipe(recipe);
                
                Log.Information("[ChannelViewModel #{Id}]: Recipe '{Name}' applied successfully",
                    ChannelId, recipe.Name);
                _dialogService.ShowInformation($"Recipe '{recipe.Name}' with ID {recipe.DatabaseId} applied successfully for channel {ChannelName}");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[ChannelViewModel #{Id}]: Failed to apply recipe '{Name}'",
                    ChannelId, recipe.Name);
                _dialogService.ShowError($"Failed to apply recipe: {exception.Message}");
            }
        }

        private void UpdateSpectrumAnnotations()
        {
            var wavelengths = new List<double>();
            var colors = new List<Color>();
            var isRecipeFlags = new List<bool>();

            var selectedCatalogLines = SpectralLinesCatalogViewModel
                .SelectedSpectralLines
                .ToList();

            foreach (var line in selectedCatalogLines)
            {
                wavelengths.Add(line.Wavelength);
                colors.Add(line.LineColor);
                isRecipeFlags.Add(false);
            }

            if (Recipe != null)
            {
                foreach (var wavelength in Recipe.Wavelengths)
                {
                    wavelengths.Add(wavelength);
                    isRecipeFlags.Add(true);
                }

                foreach (var color in Recipe.WavelengthColors)
                {
                    colors.Add(color);
                }
            }

            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                SpectrumChartViewModel.UpdateAnnotations(wavelengths, colors, isRecipeFlags);
            }, DispatcherPriority.Render);
        }

        private void OnNoiseFloorResult(SpectrumScannerResult result)
        {
            if (Application.Current?.Dispatcher.HasShutdownStarted == true || _isDisposed)
            {
                return;
            }

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                SpectrumScanChartViewModel.Update(result);
            }, DispatcherPriority.Render);
        }

        #endregion

        #region disposing

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            
            _isDisposed = true;

            Log.Debug("[ChannelViewModel #{Id}]: Disposing", ChannelId);

            WeakReferenceMessenger.Default.UnregisterAll(this);

            if (_orchestrator is not null &&
                ProcessChartViewModel is not null &&
                SpectrumChartViewModel is not null &&
                SpectrometerControlViewModel is not null)
            {
                SpectrumScanChartViewModel.Dispose();
                SpectrumChartViewModel.Dispose();
                ProcessChartViewModel.Dispose();
                SpectrumChartViewModel.OnWavelengthMoved -= (index, newWavelength) =>
                {
                    _orchestrator?.UpdateWavelengthManually(index, newWavelength);
                };
                _spectrumScanner.ResultReady -= OnNoiseFloorResult;
                _spectrumScanner.Dispose();
                _orchestrator?.Dispose();
            }

            Log.Debug("[ChannelViewModel #{Id}]: Disposed", ChannelId);
        }

        #endregion
    }
}
