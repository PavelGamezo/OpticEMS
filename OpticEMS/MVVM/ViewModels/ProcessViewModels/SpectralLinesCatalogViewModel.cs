using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Contracts.Services.Dialog;
using OpticEMS.MVVM.Models.Process;
using System.Collections.ObjectModel;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class SpectralLinesCatalogViewModel : ObservableObject
    {
        private readonly ISpectralLineRepository _spectralLineRepository;
        private readonly IDialogService _dialogService;
        private readonly int _channelId;

        private List<SpectralLineModel> _allLines = new();

        public SpectralLinesCatalogViewModel(
            int channelId,
            ISpectralLineRepository spectralLineRepository,
            IDialogService dialogService)
        {
            _channelId = channelId;
            _spectralLineRepository = spectralLineRepository;
            _dialogService = dialogService;

            SelectedElement = "O";
            SelectedIonization = "I";
            MinWavelength = 200;
            MaxWavelength = 800;

            _ = Initialize();
        }

        private async Task Initialize()
        {
            await LoadDataAsync();
            UpdateAvailableElements();
            UpdateAvailableIonizations();
            ApplyFilters();
        }

        public IEnumerable<SpectralLineModel> SelectedSpectralLines 
            => SpectralLines.Where(line => line.IsSelected);

        [ObservableProperty]
        private ObservableCollection<SpectralLineModel> spectralLines = new();

        [ObservableProperty]
        private ObservableCollection<string> availableElements = new();

        [ObservableProperty]
        private string selectedElement;

        [ObservableProperty]
        private ObservableCollection<string> availableIonizations = new();

        [ObservableProperty]
        private string selectedIonization;

        [ObservableProperty]
        private double minWavelength;

        [ObservableProperty]
        private double maxWavelength;


        private async Task LoadDataAsync()
        {
            try
            {
                var lines = await _spectralLineRepository.GetLinesAsync();

                _allLines = lines.Select(dbLine => new SpectralLineModel(_channelId)
                {
                    Id = dbLine.Id,
                    Element = dbLine.Element,
                    Ionization = dbLine.Ionization,
                    Wavelength = dbLine.Wavelength,
                    ColorHex = dbLine.ColorHex ?? "#3498DB"
                }).ToList();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Data load error: {ex.Message}");
            }
        }

        private void ApplyFilters()
        {
            if (_allLines.Count == 0)
                return;

            var filtered = _allLines
                .Where(l => l.Wavelength >= MinWavelength && l.Wavelength <= MaxWavelength)
                .Where(l => SelectedElement == null || l.Element == SelectedElement)
                .Where(l => SelectedIonization == null || l.Ionization == SelectedIonization)
                .ToList();

            SpectralLines.Clear();
            foreach (var line in filtered)
                SpectralLines.Add(line);
        }

        private void UpdateAvailableElements()
        {
            AvailableElements.Clear();

            foreach (var el in _allLines.Select(l => l.Element).Distinct().OrderBy(x => x))
                AvailableElements.Add(el);

            if (!AvailableElements.Contains(SelectedElement))
            {
                SelectedElement = AvailableElements.FirstOrDefault();
            }
        }

        private void UpdateAvailableIonizations()
        {
            AvailableIonizations.Clear();

            foreach (var ion in _allLines.Select(l => l.Ionization).Distinct().OrderBy(x => x))
                AvailableIonizations.Add(ion);

            if (!AvailableIonizations.Contains(SelectedIonization))
                SelectedIonization = AvailableIonizations.FirstOrDefault();
        }

        partial void OnSelectedElementChanged(string value)
        {
            ApplyFilters();
        }

        partial void OnSelectedIonizationChanged(string value)
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
    }
}
