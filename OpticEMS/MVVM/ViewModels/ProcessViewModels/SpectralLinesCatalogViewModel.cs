using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Contracts.Services.Dialog;
using OpticEMS.MVVM.Models.Process;
using OpticEMS.Notifications.Messages;
using Serilog;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class SpectralLinesCatalogViewModel : ObservableObject
    {
        private const string ELEMENTS_OPTION = "All";

        #region Services

        private readonly ISpectralLineRepository _spectralLineRepository;
        private readonly IDialogService _dialogService;

        #endregion

        #region Fields

        private readonly int _channelId;
        private List<SpectralLineModel> _allLines = new();
        private readonly Dictionary<int, double> _originalWavelengths = new();

        #endregion

        #region Ctor

        public SpectralLinesCatalogViewModel(
            int channelId,
            ISpectralLineRepository spectralLineRepository,
            IDialogService dialogService)
        {
            _channelId = channelId;
            _spectralLineRepository = spectralLineRepository;
            _dialogService = dialogService;

            SelectedElement = ELEMENTS_OPTION;
            MinWavelength = 200;
            MaxWavelength = 800;

            _ = Initialize();
        }

        #endregion

        #region Props

        public IEnumerable<SpectralLineModel> SelectedSpectralLines
            => _allLines.Where(line => line.IsSelected);

        #endregion

        #region ObservableProps

        [ObservableProperty]
        private ObservableCollection<SpectralLineModel> spectralLines = new();

        [ObservableProperty]
        private ObservableCollection<string> availableElements = new();

        [ObservableProperty]
        private string selectedElement;

        [ObservableProperty]
        private double minWavelength;

        [ObservableProperty]
        private double maxWavelength;

        [ObservableProperty]
        private bool _isAddFormVisible = false;

        [ObservableProperty]
        private ObservableCollection<string> _pureElementsList = new();

        [ObservableProperty]
        private string _newElement = string.Empty;

        [ObservableProperty]
        private string _newWavelengthText = string.Empty;

        [ObservableProperty]
        private Color _newLineColor = Colors.Orange;

        #endregion

        #region RelayCommands

        [RelayCommand]
        private void OpenAddForm()
        {
            PureElementsList.Clear();

            foreach (var el in AvailableElements.Where(e => e != ELEMENTS_OPTION))
            {
                PureElementsList.Add(el);
            }

            if (!string.IsNullOrEmpty(SelectedElement) && SelectedElement != ELEMENTS_OPTION)
            {
                NewElement = SelectedElement;
            }
            else
            {
                NewElement = PureElementsList.FirstOrDefault() ?? "Unknown";
            }

            IsAddFormVisible = true;
        }

        [RelayCommand]
        private void CloseAddForm()
        {
            IsAddFormVisible = false;
            NewWavelengthText = string.Empty;
        }

        [RelayCommand]
        private async Task DeleteLine(SpectralLineModel lineToDelete)
        {
            try
            {
                Log.Information("[SpectralLineCatalogViewModel]: Execute delete spectral line requested...");
                var successDelete = _spectralLineRepository.ExecuteDeleteLine(lineToDelete.Id);
                if (successDelete)
                {
                    Log.Information($"[SpectralLineCatalogViewModel]: Spectral line  with ID {lineToDelete.Id} deleted successfully");
                    
                    var lineInAll = _allLines.FirstOrDefault(l => l.Id == lineToDelete.Id);
                    if (lineInAll != null)
                    {
                        _allLines.Remove(lineInAll);
                    }

                    lineToDelete.PropertyChanged -= OnLinePropertyChanged;

                    WeakReferenceMessenger.Default.Send(new LinesChangedMessage());

                    UpdateAvailableElements();
                    ApplyFilters();

                    Log.Information("[SpectralLineCatalogViewModel]: Spectral line model deleted from cache successfully.");
                }
                else
                {
                    Log.Warning("[SpectralLineCatalogViewModel]: Spectral line was not found or could not be deleted.");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Error during spectral line removing: {ex.Message}");
                Log.Error(ex, "[SpectralLineCatalogViewModel]: Error during spectral line removing.");
            }
        }

        [RelayCommand(CanExecute = nameof(CanSaveNewLine))]
        private async Task SaveNewLine()
        {
            Log.Information("[SpectralLineCatalogViewModel]: Request spectral line saving.");
            string formattedText = NewWavelengthText.Replace(',', '.').Trim();

            if (double.TryParse(formattedText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedWavelength))
            {
                if (parsedWavelength < 100 || parsedWavelength > 1000)
                {
                    _dialogService.ShowError("Wavelength out of bounds (100 - 1000 nm)");
                    return;
                }

                string hexColor = $"#{NewLineColor.R:X2}{NewLineColor.G:X2}{NewLineColor.B:X2}";

                var newLine = new SpectralLineModel(_channelId)
                {
                    Id = _allLines.Count > 0 ? _allLines.Max(l => l.Id) + 1 : 1,
                    Element = NewElement,
                    Wavelength = parsedWavelength,
                    ColorHex = hexColor,
                    IsSelected = true
                };

                var spectralLine = new SpectralLine()
                {
                    Id = newLine.Id,
                    Element = newLine.Element,
                    Wavelength = newLine.Wavelength,
                    ColorHex = newLine.ColorHex
                };

                try
                {
                    Log.Information("[SpectralLineCatalogViewModel]: Saving spectral line in database...");
                    await _spectralLineRepository.AddLineAsync(spectralLine, CancellationToken.None);
                    await _spectralLineRepository.SaveChangesAsync(CancellationToken.None);
                    Log.Information("[SpectralLineCatalogViewModel]: Spectral line saved in database successfully.");
                    
                    _allLines.Add(newLine);

                    WeakReferenceMessenger.Default.Send(new LinesChangedMessage());
                    await LoadDataAsync();

                    UpdateAvailableElements();
                    ApplyFilters();

                    IsAddFormVisible = false;
                    NewWavelengthText = string.Empty;
                    NewElement = string.Empty;
                }
                catch (Exception exception)
                {
                    _dialogService.ShowError($"Error during spectral line saving: {exception.Message}");
                    Log.Error(exception, "[SpectralLineCatalogViewModel]: Error during spectral line saving.");
                }
            }
        }

        #endregion

        #region Methods

        private async Task Initialize()
        {
            Log.Information("[SpectralLineCatalogViewModel]: Spectral line catalog initializing started...");
            await LoadDataAsync();
            UpdateAvailableElements();
            ApplyFilters();
            Log.Information("[SpectralLineCatalogViewModel]: Spectral line catalog initializing cancelled successfully.");
        }

        private bool CanSaveNewLine()
        {
            return !string.IsNullOrWhiteSpace(NewWavelengthText) && !string.IsNullOrEmpty(NewElement);
        }

        private async Task LoadDataAsync()
        {
            try
            {
                Log.Information("[SpectralLineCatalogViewModel]: Loading spectral lines from database...");
                var lines = await _spectralLineRepository.GetLinesAsync();

                _originalWavelengths.Clear();

                _allLines = lines.Select(dbLine =>
                {
                    _originalWavelengths[dbLine.Id] = dbLine.Wavelength;
                    return new SpectralLineModel(_channelId)
                    {
                        Id = dbLine.Id,
                        Element = dbLine.Element,
                        Wavelength = dbLine.Wavelength,
                        ColorHex = dbLine.ColorHex ?? "#3498DB"
                    };
                }).ToList();

                Log.Information($"[SpectralLineCatalogViewModel]: Spectral lines loading cancelled with {lines.Count} count." +
                    $"Initialized {_allLines.Count} spectral line models.");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[SpectralLineCatalogViewModel]: Error during spectral lines data loading.");
                _dialogService.ShowError($"Data load error: {exception.Message}");
            }
        }

        private void ApplyFilters()
        {
            try
            {
                foreach (var line in SpectralLines)
                {
                    line.PropertyChanged -= OnLinePropertyChanged;
                }

                if (_allLines == null || _allLines.Count == 0)
                {
                    SpectralLines.Clear();
                    return;
                }

                var filtered = _allLines
                    .Where(l => l.Wavelength >= MinWavelength && l.Wavelength <= MaxWavelength)
                    .Where(l => string.IsNullOrEmpty(SelectedElement) ||
                                SelectedElement == ELEMENTS_OPTION ||
                                l.Element == SelectedElement)
                    .ToList();

                SpectralLines.Clear();

                foreach (var line in filtered)
                {
                    line.PropertyChanged += OnLinePropertyChanged;
                    SpectralLines.Add(line);
                }

                Log.Information("[SpectralLineCatalogViewModel]: Spectral line filters applied successfully.");
            }
            catch (Exception exception)
            {
                _dialogService.ShowError("Unexpected error during spectral line filters applying");
                Log.Error(exception, "Unexpected error during spectral line filters applying.");
            }
        }

        public double GetOriginalWavelength(int lineId)
        {
            return _originalWavelengths.TryGetValue(lineId, out double originalValue)
                ? originalValue
                : 0;
        }

        private void UpdateAvailableElements()
        {
            try
            {
                Log.Information("[SpectralLineCatalogViewModel]: Finding independent available elements.");
                AvailableElements.Clear();

                AvailableElements.Add(ELEMENTS_OPTION);

                foreach (var el in _allLines
                    .Select(l => l.Element)
                    .Distinct()
                    .OrderBy(x => x))
                {
                    AvailableElements.Add(el);
                }

                Log.Information($"[SpectralLineCatalogViewModel]: Found {AvailableElements.Count} available elements.");

                if (string.IsNullOrEmpty(SelectedElement) || !AvailableElements.Contains(SelectedElement))
                {
                    SelectedElement = ELEMENTS_OPTION;
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[SpectralLineCatalogViewModel]: Unexpected error during updating lines elements");
            }
        }

        private async Task UpdateLineInDatabaseAsync(SpectralLineModel model)
        {
            try
            {
                Log.Information($"[SpectralLineCatalogViewModel]: Spectral line with ID {model.Id} update request.");
                var line = await _spectralLineRepository.GetLineByIdAsync(model.Id, CancellationToken.None);

                if (line != null)
                {
                    line.Element = model.Element;
                    line.Wavelength = model.Wavelength;
                    line.ColorHex = model.ColorHex;

                    Log.Information("[SpectralLineCatalogViewModel]: Execute update request...");
                    var updateRequest = _spectralLineRepository.ExecuteUpdateLine(line);
                    if (updateRequest)
                    {
                        WeakReferenceMessenger.Default.Send(new LinesChangedMessage());

                        Log.Information("[SpectralLineCatalogViewModel]: Execute update request successed.");
                    }

                    Log.Information($"[SpectralLineCatalogViewModel]: Spectral line with ID {model.Id} updated successfully");
                }
                else
                {
                    Log.Warning($"[SpectralLineCatalogViewModel]: Can't find spectral line with ID {model.Id}");
                }
            }
            catch (Exception exception)
            {
                _dialogService.ShowError($"Error during spectral line update: {exception.Message}");
                Log.Error(exception, "[SpectralLineCatalogViewModel]: Error during spectral line update.");
            }
        }

        #endregion

        #region Triggers

        private void OnLinePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is not SpectralLineModel editedLine)
            {
                return;
            }

            if (e.PropertyName == nameof(SpectralLineModel.Element) ||
                e.PropertyName == nameof(SpectralLineModel.Wavelength))
            {
                _ = UpdateLineInDatabaseAsync(editedLine);

                WeakReferenceMessenger.Default.Send(new LinesChangedMessage());

                if (editedLine.IsSelected)
                {
                    WeakReferenceMessenger.Default.Send(
                        new SpectralLineSelectionMessage(_channelId, editedLine.Wavelength, editedLine.ColorHex));
                }
            }

            if (e.PropertyName == nameof(SpectralLineModel.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedSpectralLines));
                
                WeakReferenceMessenger.Default.Send(new LinesChangedMessage());
                WeakReferenceMessenger.Default.Send(
                    new SpectralLineSelectionMessage(_channelId, editedLine.Wavelength, editedLine.ColorHex));
            }
        }

        partial void OnNewWavelengthTextChanged(string value)
        {
            SaveNewLineCommand.NotifyCanExecuteChanged();
        }

        partial void OnNewElementChanged(string value)
        {
            SaveNewLineCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedElementChanged(string value)
        {
            ApplyFilters();
        }

        partial void OnMinWavelengthChanged(double value)
        {
            ApplyFilters();
        }

        partial void OnMaxWavelengthChanged(double value)
        {
            ApplyFilters();
        }

        #endregion
    }
}
