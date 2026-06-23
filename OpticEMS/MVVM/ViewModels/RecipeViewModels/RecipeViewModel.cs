using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Contracts.Services.Dialog;
using OpticEMS.Contracts.Services.ProcessingModes;
using OpticEMS.Contracts.Services.Recipe;
using OpticEMS.MVVM.Models;
using OpticEMS.Notifications.Messages;
using OpticEMS.Notifications.Messages.SpectralLines;
using OpticEMS.Services.Validators;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OpticEMS.MVVM.ViewModels.RecipeViewModels
{
    public partial class RecipeViewModel : ObservableObject
    {
        private readonly IRecipeRepository _recipeRepository;
        private readonly ISpectralLineRepository _spectralLineRepository;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private Color _currentWavelengthColor;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteRecipeCommand))]
        [NotifyCanExecuteChangedFor(nameof(RenameRecipeCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveRecipeCommand))]
        [NotifyCanExecuteChangedFor(nameof(AddWavelengthCommand))]
        private Recipe? _selectedRecipe;

        [ObservableProperty]
        private ObservableCollection<Recipe> _recipeFiles = new();

        [ObservableProperty]
        private int _recipeCount;

        [ObservableProperty]
        private string _searchText;

        [ObservableProperty]
        private string _selectedOverEtchText = "No";

        [ObservableProperty]
        private string _selectedAutocalibrationText = "No";

        [ObservableProperty]
        private string _selectedPcaText = "No";

        [ObservableProperty]
        private string _selectedDerivativeText = "No";

        [ObservableProperty]
        private bool _isSingleWindowMode;

        [ObservableProperty]
        private ObservableCollection<SpectralLine> _spectralLines = new();

        [ObservableProperty]
        private ObservableCollection<WavelengthMonitorItem> _wavelengthItems = new();

        [ObservableProperty]
        private ProcessingMode _processingMode;

        [ObservableProperty]
        private DualChannelSubMode _dualSubMode;

        [ObservableProperty]
        private bool _isDualChannelMode;

        private bool _isSuppressingSync = false;

        public List<ProcessingMode> AvailableProcessingModes { get; private set; } = new();

        public ObservableCollection<string> AvailableChannelNames { get; } = new();

        public List<DualChannelSubMode> DualSubModes { get; } =
            Enum.GetValues(typeof(DualChannelSubMode)).Cast<DualChannelSubMode>().ToList();

        public ICollectionView RecipesView { get; private set; }
        public int SelectedWavelengthIndex { get; set; }

        public List<string> YesNoOptions { get; } = new() { "Yes", "No" };
        public List<string> ProcessChambers { get; } = new()
        {
            "Chamber A", "Chamber B", "Chamber C", "Chamber D"
        };

        public Action<Recipe>? ApplyRecipeRequested { get; set; }

        private bool HasSelectedRecipe => SelectedRecipe != null;

        public double CommonSignalHigh
        {
            get => WavelengthItems.FirstOrDefault()?.SignalHigh ?? 0;
            set
            {
                foreach (var item in WavelengthItems)
                {
                    item.SignalHigh = value;
                }

                OnPropertyChanged(nameof(CommonSignalHigh));
            }
        }

        public int CommonWindowIn
        {
            get => WavelengthItems.FirstOrDefault()?.WindowInCount ?? 0;
            set
            {
                foreach (var item in WavelengthItems)
                {
                    item.WindowInCount = value;
                }

                OnPropertyChanged(nameof(CommonWindowIn));
            }
        }

        public int CommonWindowOut
        {
            get => WavelengthItems.FirstOrDefault()?.WindowOutCount ?? 0;
            set
            {
                foreach (var item in WavelengthItems)
                {
                    item.WindowOutCount = value;
                }

                OnPropertyChanged(nameof(CommonWindowOut));
            }
        }

        public int CommonWindowTime
        {
            get => WavelengthItems.FirstOrDefault()?.DetectionWindowTime ?? 0;
            set
            {
                foreach (var item in WavelengthItems)
                {
                    item.DetectionWindowTime = value;
                }

                OnPropertyChanged(nameof(CommonWindowTime));
            }
        }

        public RecipeViewModel(
            IRecipeRepository recipeRepository,
            ISpectralLineRepository spectralLineRepository,
            IDialogService dialogService,
            IExpressionValidator validator)
        {
            try
            {
                _recipeRepository = recipeRepository;
                _spectralLineRepository = spectralLineRepository;
                _dialogService = dialogService;

                SetupWavelengthItemsListener();
                RegisterMessages();

                _ = InitializeDataAsync();
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, "[RecipeViewModel]: Critical failure during startup.");
            }
        }

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<SaveSpectralLineMessage>(this, async (r, m) =>
            {
                await SaveNewSpectralLineAsync(m.Element, m.WavelengthText, m.HexColor);
            });


            WeakReferenceMessenger.Default.Register<LinesChangedMessage>(this, (recipient, message) =>
            {
                _ = InitializeDataAsync();
            });
        }

        private async Task SaveNewSpectralLineAsync(string element, string wavelengthText, string hexColor)
        {
            if (string.IsNullOrWhiteSpace(element))
            {
                _dialogService.ShowError("Please enter an element name.");
                return;
            }

            try
            {
                string cleanedWavelength = wavelengthText.Replace(',', '.').Trim();
                if (!double.TryParse(cleanedWavelength, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double wavelength) || wavelength <= 0)
                {
                    _dialogService.ShowError("Please enter a valid positive wavelength (λ).");
                    return;
                }

                var newLine = new SpectralLine
                {
                    Element = element.Trim(),
                    Wavelength = wavelength,
                    ColorHex = hexColor
                };

                Log.Information("[RecipeViewModel]: Adding new spectral line {Element} ({Wavelength} nm) to database.", newLine.Element, newLine.Wavelength);

                await _spectralLineRepository.AddLineAsync(newLine, CancellationToken.None);
                await _spectralLineRepository.SaveChangesAsync(CancellationToken.None);

                WeakReferenceMessenger.Default.Send(new LinesChangedMessage());

                Log.Information("[RecipeViewModel]: New spectral line saved successfully.");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[RecipeViewModel]: Failed to save new spectral line.");
                _dialogService.ShowError($"Error saving spectral line: {exception.Message}");
            }
        }

        private void SetupWavelengthItemsListener()
        {
            WavelengthItems.CollectionChanged += (_, _) => UpdateModesAfterWavelengthChange();
        }

        private void UpdateModesAfterWavelengthChange()
        {
            if (_isSuppressingSync)
            {
                return;
            }

            SyncToModel();

            if (SelectedRecipe == null)
            {
                return;
            }

            SelectedRecipe.AutoConfigureMode();

            UpdateAvailableModes();

            if (!AvailableProcessingModes.Contains(ProcessingMode))
            {
                ProcessingMode = AvailableProcessingModes.FirstOrDefault(ProcessingMode.SingleChannel);
            }

            SelectedRecipe.ProcessingMode = ProcessingMode;
            SelectedRecipe.DualSubMode = DualSubMode;

            UpdateModeVisibility();
            UpdateAvailableChannelNames();
        }

        private void UpdateAvailableChannelNames()
        {
            AvailableChannelNames.Clear();
            if (SelectedRecipe != null)
            {
                foreach (var name in SelectedRecipe.WavelengthNames)
                {
                    AvailableChannelNames.Add(name);
                }
            }
        }

        private void UpdateAvailableModes()
        {
            AvailableProcessingModes.Clear();

            if (SelectedRecipe == null)
            {
                return;
            }

            int count = SelectedRecipe.Wavelengths.Count;

            if (count <= 1)
            {
                AvailableProcessingModes.Add(ProcessingMode.SingleChannel);
                ProcessingMode = ProcessingMode.SingleChannel;
            }
            else if (count >= 2)
            {
                AvailableProcessingModes.Add(ProcessingMode.DualChannel);
                if (ProcessingMode != ProcessingMode.DualChannel)
                {
                    ProcessingMode = ProcessingMode.DualChannel;
                }
            }
        }

        private void UpdateModeVisibility()
        {
            IsDualChannelMode = ProcessingMode == ProcessingMode.DualChannel;
        }

        [RelayCommand(CanExecute = nameof(HasSelectedRecipe))]
        private void ApplySelectedRecipe()
        {
            Log.Information("[RecipeViewMode]: Applying selected recipe requested.");
            ApplyRecipeRequested?.Invoke(SelectedRecipe);
        }

        [RelayCommand]
        private async Task NewRecipe()
        {
            try
            {
                var name = _dialogService.ShowRenameQuestion("NewRecipe");

                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                var newRecipe = new Recipe
                {
                    Name = name,
                    CreatedAt = DateTime.Now,
                    LastModifiedAt = DateTime.Now,
                    Wavelengths = new List<double>(),
                    WavelengthColors = new List<Color>(),
                    DetectionWindowHighs = new List<double>()
                };

                Log.Information("[RecipeViewModel]: Creating and saving new recipe: {Name}", name);

                await _recipeRepository.AddRecipeAsync(newRecipe);
                await _recipeRepository.SaveChangesAsync();

                await LoadFilesAsync(newRecipe.Name);
            }
            catch (Exception exception)
            {
                Log.Fatal("[RecipeViewModel]: Critical failure during recipe saving.");
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedRecipe))]
        private async Task DeleteRecipe()
        {
            try
            {
                var confirmed = _dialogService.AskWarningQuestion("Would you like to delete selected file?");

                if (confirmed == true)
                {
                    _recipeRepository.RemoveRecipe(SelectedRecipe);
                    await _recipeRepository.SaveChangesAsync();

                    Log.Information("[RecipeViewModel]: Recipe deleted successfully.");

                    await LoadFilesAsync();
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[RecipeViewModel]: Failed to delete recipe '{Name}'", SelectedRecipe?.Name);
                _dialogService.ShowError(exception.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedRecipe))]
        private async Task RenameRecipe()
        {
            try
            {
                var newName = _dialogService.ShowRenameQuestion(SelectedRecipe.Name);

                if (!string.IsNullOrWhiteSpace(newName) && newName != SelectedRecipe.Name)
                {
                    string oldName = SelectedRecipe.Name;
                    SelectedRecipe.Name = newName;
                    SelectedRecipe.LastModifiedAt = DateTime.Now;

                    Log.Information("[RecipeViewModel]: Recipe renamed successfully.");
                    await _recipeRepository.UpdateRecipeAsync(SelectedRecipe);
                    await _recipeRepository.SaveChangesAsync();

                    await LoadFilesAsync();
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[RecipeViewModel]: Failed to rename recipe '{Old}'",
                    SelectedRecipe?.Name);
                _dialogService.ShowError(exception.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedRecipe))]
        private void AddWavelength()
        {
            var newItem = new WavelengthMonitorItem(500.0, Colors.Cyan, 0, 1000, 1, 1);
            newItem.PropertyChanged += OnItemPropertyChanged;

            WavelengthItems.Add(newItem);
        }

        [RelayCommand]
        private void RemoveWavelength(WavelengthMonitorItem item)
        {
            WavelengthItems.Remove(item);
        }

        [RelayCommand(CanExecute = nameof(HasSelectedRecipe))]
        private async Task SaveRecipe()
        {
            SyncToModel();

            try
            {
                SelectedRecipe.ProcessingMode = ProcessingMode;
                SelectedRecipe.DualSubMode = DualSubMode;

                await _recipeRepository.UpdateRecipeAsync(SelectedRecipe);
                await _recipeRepository.SaveChangesAsync();
                
                Log.Information("[RecipeViewModel]: Recipe '{Name}' saved. Wavelengths={Count}",
                    SelectedRecipe.Name, SelectedRecipe.Wavelengths.Count);
                _dialogService.ShowInformation("Recipe saved successfully.");

                await LoadFilesAsync();
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[RecipeViewModel]: Failed to save recipe '{Name}'", SelectedRecipe?.Name);
                _dialogService.ShowError(exception.Message);
            }
        }

        private void SyncToModel()
        {
            if (SelectedRecipe == null)
            {
                return;
            }

            SelectedRecipe.Wavelengths = WavelengthItems.Select(x => x.Wavelength).ToList();
            SelectedRecipe.WavelengthColors = WavelengthItems.Select(x => x.Color).ToList();
            SelectedRecipe.DetectionWindowTimes = WavelengthItems.Select(x => x.DetectionWindowTime).ToList();
            SelectedRecipe.WindowInCounts = WavelengthItems.Select(x => x.WindowInCount).ToList();
            SelectedRecipe.WindowOutCounts = WavelengthItems.Select(x => x.WindowOutCount).ToList();
            SelectedRecipe.DetectionWindowHighs = WavelengthItems.Select(x => x.SignalHigh).ToList();
        }

        private void LoadWavelengthsToUI()
        {
            _isSuppressingSync = true;

            try
            {
                foreach (var item in WavelengthItems)
                {
                    item.PropertyChanged -= OnItemPropertyChanged;
                }

                WavelengthItems.Clear();

                if (SelectedRecipe == null)
                {
                    return;
                }

                for (int i = 0; i < SelectedRecipe.Wavelengths.Count; i++)
                {
                    var color = SelectedRecipe.WavelengthColors.Count > i
                                ? SelectedRecipe.WavelengthColors[i]
                                : Colors.White;

                    var high = SelectedRecipe.DetectionWindowHighs.Count > i 
                        ? SelectedRecipe.DetectionWindowHighs[i] 
                        : 0;

                    var windowIn = SelectedRecipe.WindowInCounts.Count > i 
                        ? SelectedRecipe.WindowInCounts[i] 
                        : 0;

                    var windowOut = SelectedRecipe.WindowOutCounts.Count > i 
                        ? SelectedRecipe.WindowOutCounts[i] 
                        : 0;

                    var windowTime = SelectedRecipe.DetectionWindowTimes.Count > i 
                        ? SelectedRecipe.DetectionWindowTimes[i] 
                        : 0;

                    var newItem = new WavelengthMonitorItem(SelectedRecipe.Wavelengths[i], color, high, windowTime, windowIn, windowOut);

                    var matchedLine = SpectralLines
                        .FirstOrDefault(line => Math.Abs(line.Wavelength - SelectedRecipe.Wavelengths[i]) < 2);

                    if (matchedLine != null)
                    {
                        newItem.SelectedLine = matchedLine;
                    }

                    newItem.PropertyChanged += OnItemPropertyChanged;
                    WavelengthItems.Add(newItem);
                }
            }
            finally
            {
                _isSuppressingSync = false;
                UpdateModesAfterWavelengthChange();
            }
        }

        private async Task InitializeDataAsync()
        {
            await LoadSpectralLinesAsync();
            await LoadFilesAsync();
        }

        private async Task LoadSpectralLinesAsync()
        {
            try
            {
                Log.Information("[RecipeViewModel]: Async loading of spectral lines started.");

                var lines = await _spectralLineRepository.GetLinesAsync();

                SpectralLines = new ObservableCollection<SpectralLine>(
                    lines.OrderBy(line => line.Element)
                         .ThenBy(line => line.Wavelength));

                Log.Information("[RecipeViewModel]: Async loading of spectral lines cancelled successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RecipeViewModel]: Failed to load spectral lines from DB.");
            }
        }

        private async Task LoadFilesAsync(string? nameToSelect = null)
        {
            try
            {
                Log.Information("[RecipeViewModel]: Async loading of recipes started.");

                var recipes = await _recipeRepository.GetRecipesAsync();
                var selectedName = nameToSelect ?? SelectedRecipe?.Name;

                Log.Information("[RecipeViewModel]: Recipes getted from database successfully. Started fill out recipe models");

                RecipeFiles = new ObservableCollection<Recipe>(recipes);
                RecipesView = CollectionViewSource.GetDefaultView(RecipeFiles);
                RecipesView.Filter = obj =>
                {
                    if (string.IsNullOrWhiteSpace(SearchText))
                    {
                        return true;
                    }

                    if (obj is Recipe recipe)
                    {
                        return recipe.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
                    }

                    return false;
                };

                SelectedRecipe = RecipeFiles.FirstOrDefault(recipe => recipe.Name == selectedName)
                    ?? RecipeFiles.FirstOrDefault();

                Log.Information("[RecipeViewModel]: Recipe collection successfully filled out. Get recipes count.");

                RecipeCount = RecipesView.Cast<Recipe>().Count();

                Log.Information("[RecipeViewModel]: Async loading of recipes cancelled successfully.");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[RecipeViewModel]: Failed to load recipes from DB.");
            }

            OnPropertyChanged(nameof(RecipesView));
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            SyncToModel();
        }

        partial void OnProcessingModeChanged(ProcessingMode value)
        {
            UpdateModeVisibility();
            UpdateWindowMode();
        }

        partial void OnDualSubModeChanged(DualChannelSubMode value)
        {
            UpdateModeVisibility();
            UpdateWindowMode();
        }

        private void UpdateWindowMode()
        {
            bool isCombinedOrRatio = ProcessingMode == ProcessingMode.DualChannel && DualSubMode == DualChannelSubMode.Ratio;

            IsSingleWindowMode = isCombinedOrRatio;

            if (isCombinedOrRatio && SelectedRecipe?.DetectionWindowHighs.Count > 0)
            {
                CommonSignalHigh = SelectedRecipe.DetectionWindowHighs.FirstOrDefault();
            }
        }

        partial void OnSelectedRecipeChanged(Recipe? value)
        {
            if (value != null)
            {
                SelectedWavelengthIndex = 0;
                LoadWavelengthsToUI();

                value.AutoConfigureMode();

                ProcessingMode = value.ProcessingMode;
                DualSubMode = value.DualSubMode;

                UpdateAvailableChannelNames();
                UpdateAvailableModes();
                UpdateModeVisibility();

                SelectedOverEtchText = value.OverEtchEnabled ? "Yes" : "No";
                SelectedAutocalibrationText = value.AutocalibrationEnabled ? "Yes" : "No";
                SelectedPcaText = value.PcaEnabled ? "Yes" : "No";
                SelectedDerivativeText = value.DerivativeEnabled ? "Yes" : "No";
            }
        }

        partial void OnSelectedDerivativeTextChanged(string value)
        {
            if (SelectedRecipe != null)
            {
                SelectedRecipe.DerivativeEnabled = (value == "Yes");
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            RecipesView?.Refresh();

            RecipeCount = RecipesView.Cast<object>().Count();
        }

        partial void OnCurrentWavelengthColorChanged(Color value)
        {
            if (SelectedRecipe != null &&
                SelectedWavelengthIndex >= 0 &&
                SelectedWavelengthIndex < SelectedRecipe.WavelengthColors.Count)
            {
                SelectedRecipe.WavelengthColors[SelectedWavelengthIndex] = value;
            }
        }

        partial void OnSelectedOverEtchTextChanged(string value)
        {
            if (SelectedRecipe != null)
            {
                SelectedRecipe.OverEtchEnabled = (value == "Yes");
            }
        }

        partial void OnSelectedPcaTextChanged(string value)
        {
            if (SelectedRecipe != null)
            {
                SelectedRecipe.PcaEnabled = (value == "Yes");
            }
        }

        partial void OnSelectedAutocalibrationTextChanged(string value)
        {
            if (SelectedRecipe != null)
            {
                SelectedRecipe.AutocalibrationEnabled = (value == "Yes");
            }
        }
    }
}
