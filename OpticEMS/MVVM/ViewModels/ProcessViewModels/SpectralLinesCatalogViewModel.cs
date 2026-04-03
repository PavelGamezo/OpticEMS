using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.MVVM.Models.Process;
using OpticEMS.Services.Dialogs;
using System.Collections.ObjectModel;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class SpectralLinesCatalogViewModel : ObservableObject
    {
        private readonly ISpectralLineRepository _spectralLineRepository;
        private readonly IDialogService _dialogService;

        private readonly int _channelId;

        public SpectralLinesCatalogViewModel(int channelId, 
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

            LoadData();
            LoadAvailableElements();
            LoadAvailableIonizations();
        }

        public IEnumerable<SpectralLineModel> SelectedSpectralLines
            => SpectralLines.Where(line => line.IsSelected);

        [ObservableProperty]
        private ObservableCollection<SpectralLineModel> _spectralLines = new();

        [ObservableProperty]
        private ObservableCollection<string> _availableElements = new();

        [ObservableProperty]
        private string _selectedElement;

        [ObservableProperty]
        private ObservableCollection<string> _availableIonizations = new();

        [ObservableProperty]
        private string _selectedIonization;

        [ObservableProperty]
        private double _minWavelength = 200;

        [ObservableProperty]
        private double _maxWavelength = 800;
        
        private void LoadAvailableIonizations()
        {
            AvailableIonizations.Clear();

            var availableElements = SpectralLines
                .Select(line => line.Ionization)
                .Distinct()
                .OrderBy(element => element)
                .ToList();

            foreach (var element in availableElements)
            {
                AvailableIonizations.Add(element);
            }
        }

        private void LoadAvailableElements()
        {
            AvailableElements.Clear();

            var availableElements = SpectralLines
                .Select(line => line.Element)
                .Distinct()
                .OrderBy(element => element)
                .ToList();

            foreach (var element in availableElements)
            {
                AvailableElements.Add(element);
            }
        }

        private void LoadData()
        {
            try
            {
                var lines = _spectralLineRepository.GetLines();

                SpectralLines.Clear();

                foreach (var dbLine in lines)
                {
                    var model = new SpectralLineModel(_channelId)
                    {
                        Id = dbLine.Id,
                        Element = dbLine.Element,
                        Ionization = dbLine.Ionization,
                        Wavelength = dbLine.Wavelength,
                        ColorHex = dbLine.ColorHex ?? "#3498DB"
                    };

                    SpectralLines.Add(model);
                }
            }
            catch (Exception exception)
            {
                _dialogService.ShowError($"Data export error: {exception.Message}");
            }
        }

        private void LoadDataByChangingWavelength()
        {
            try
            {
                var lines = _spectralLineRepository.GetLinesByRange(MinWavelength, MaxWavelength);

                SpectralLines.Clear();

                foreach (var dbLine in lines)
                {
                    var model = new SpectralLineModel(_channelId)
                    {
                        Id = dbLine.Id,
                        Element = dbLine.Element,
                        Ionization = dbLine.Ionization,
                        Wavelength = dbLine.Wavelength,
                        ColorHex = dbLine.ColorHex ?? "#3498DB"
                    };

                    SpectralLines.Add(model);
                }
            }
            catch (Exception exception)
            {
                _dialogService.ShowError($"Data export error: {exception.Message}");
            }
        }

        private void LoadDataByChangingElement()
        {
            try
            {
                var lines = _spectralLineRepository.GetLinesByElement(SelectedElement);

                SpectralLines.Clear();

                foreach (var dbLine in lines)
                {
                    var model = new SpectralLineModel(_channelId)
                    {
                        Id = dbLine.Id,
                        Element = dbLine.Element,
                        Ionization = dbLine.Ionization,
                        Wavelength = dbLine.Wavelength,
                        ColorHex = dbLine.ColorHex ?? "#3498DB"
                    };

                    SpectralLines.Add(model);
                }
            }
            catch (Exception exception)
            {
                _dialogService.ShowError($"Data export error: {exception.Message}");
            }
        }

        private void LoadDataByChangingIonization()
        {
            try
            {
                var lines = _spectralLineRepository.GetLinesByElement(SelectedIonization);

                SpectralLines.Clear();

                foreach (var dbLine in lines)
                {
                    var model = new SpectralLineModel(_channelId)
                    {
                        Id = dbLine.Id,
                        Element = dbLine.Element,
                        Ionization = dbLine.Ionization,
                        Wavelength = dbLine.Wavelength,
                        ColorHex = dbLine.ColorHex ?? "#3498DB"
                    };

                    SpectralLines.Add(model);
                }
            }
            catch (Exception exception)
            {
                _dialogService.ShowError($"Data export error: {exception.Message}");
            }
        }

        partial void OnSelectedElementChanged(string value) => LoadDataByChangingElement();

        partial void OnSelectedIonizationChanged(string value) => LoadDataByChangingIonization();

        partial void OnMinWavelengthChanged(double value) => LoadDataByChangingWavelength();

        partial void OnMaxWavelengthChanged(double value) => LoadDataByChangingWavelength();

    }
}
