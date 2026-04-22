using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticEMS.Services.Dialogs;
using OpticEMS.Services.Files;
using OpticEMS.MVVM.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;
using OpticEMS.MVVM.Models.Recipe;
using Serilog;

namespace OpticEMS.MVVM.ViewModels.RecipeViewModels
{
    public partial class RecipeViewModel : ObservableObject
    {
        private readonly IRecipeFileManager _recipeFileManager;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private Color _currentWavelengthColor;

        [ObservableProperty]
        private RecipeModel? _selectedRecipe;

        [ObservableProperty]
        private ObservableCollection<RecipeModel> _recipeFiles = new();

        [ObservableProperty]
        private int _recipeCount;

        [ObservableProperty]
        private string _searchText;

        [ObservableProperty]
        private string _selectedOverEtchText = "No";

        [ObservableProperty]
        private string _selectedAutocalibrationText = "No";

        [ObservableProperty]
        private string _selectedPCAText = "No";

        [ObservableProperty]
        private ObservableCollection<WavelengthMonitorItem> _wavelengthItems = new();

        public Action<RecipeModel>? ApplyRecipeRequested { get; set; }

        public ICollectionView RecipesView { get; private set; }

        public int SelectedWavelengthIndex { get; set; }

        public List<string> YesNoOptions { get; } = new() { "Yes", "No" };

        public List<string> ProcessChambers { get; } = new() 
        { 
            "Chamber A", "Chamber B", "Chamber C", "Chamber D" 
        };

        private bool HasSelectedRecipe => SelectedRecipe != null;

        public RecipeViewModel(IRecipeFileManager recipeFileManager,
            IDialogService dialogService)
        {
            try
            {
                _recipeFileManager = recipeFileManager;
                _dialogService = dialogService;
                _ = LoadFilesAsync();
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, "RecipeViewModel: Critical failure during startup.");
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedRecipe))]
        private void ApplySelectedRecipe()
        {
            Log.Information("RecipeViewMode: Applying selected recipe requested.");
            ApplyRecipeRequested?.Invoke(SelectedRecipe);
        }

        [RelayCommand] 
        private async Task NewRecipe() 
        {
            try
            {
                if (SelectedRecipe is null)
                {
                    SelectedRecipe = new RecipeModel();
                    Log.Information("RecipeViewMode: Created new empty recipe file.");
                }

                var name = _dialogService.ShowRenameQuestion("NewRecipe");

                if (!string.IsNullOrEmpty(name))
                {
                    var newRecipe = new RecipeModel
                    {
                        Name = name,
                        CreatedAt = DateTime.Now,
                        LastModifiedAt = DateTime.Now,
                    };

                    Log.Information("RecipeViewMode: Created new recipe file.");

                    SelectedRecipe = newRecipe;

                    await _recipeFileManager.SaveRecipe(newRecipe);
                    await LoadFilesAsync();
                }
            }
            catch (Exception exception)
            {
                Log.Fatal("RecipeViewModel: Critical failure during recipe saving.");
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
                    _recipeFileManager.DeleteRecipe(SelectedRecipe.Name);
                    Log.Information("RecipeViewModel: Recipe deleted successfully.");

                    await LoadFilesAsync();
                }
            }
            catch (Exception exception) 
            {
                Log.Fatal(exception, "RecipeViewModel: Error during recipe deleting.");
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

                    Log.Information("RecipeViewModel: Recipe renamed successfully.");
                    await _recipeFileManager.RenameRecipe(oldName, SelectedRecipe);

                    await LoadFilesAsync();
                }
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, "RecipeViewModel: Fatal error during recipe renaming");
                _dialogService.ShowError(exception.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedRecipe))]
        private void AddWavelength()
        {
            var newItem = new WavelengthMonitorItem(500.0, Colors.Cyan, 0);
            newItem.PropertyChanged += OnItemPropertyChanged;

            WavelengthItems.Add(newItem);
            SyncToModel();
        }

        [RelayCommand]
        private void RemoveWavelength(WavelengthMonitorItem item)
        {
            WavelengthItems.Remove(item);
            SyncToModel();
        }

        [RelayCommand(CanExecute = nameof(HasSelectedRecipe))]
        private async Task SaveRecipe()
        {
            try
            {
                SelectedRecipe.LastModifiedAt = DateTime.Now;
                await _recipeFileManager.SaveRecipe(SelectedRecipe);

                Log.Information("RecipeViewModel: Recipe saved successfully.");
                _dialogService.ShowInformation("Recipe saved successfully.");
                await LoadFilesAsync();
            }
            catch (Exception exception)
            {
                Log.Information(exception, "RecipeViewModel: Fatal error during recipe saving");
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
            SelectedRecipe.DetectionWindowHighs = WavelengthItems.Select(x => x.SignalHigh).ToList();
        }

        private void LoadWavelengthsToUI()
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

                var high = SelectedRecipe.DetectionWindowHighs.Count > i ? SelectedRecipe.DetectionWindowHighs[i] : 0;

                var newItem = new WavelengthMonitorItem(SelectedRecipe.Wavelengths[i], color, high);

                newItem.PropertyChanged += OnItemPropertyChanged;
                WavelengthItems.Add(newItem);
            }
        }

        private async Task LoadFilesAsync(string? nameToSelect = null) 
        {
            var files = await _recipeFileManager.LoadRecipeFiles();

            var selectedName = nameToSelect ?? SelectedRecipe?.Name;

            RecipeFiles = new ObservableCollection<RecipeModel>(files);

            RecipesView = CollectionViewSource.GetDefaultView(RecipeFiles);
            RecipesView.Filter = obj =>
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    return true;
                }

                if (obj is RecipeModel recipe)
                {
                    return recipe.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
                }

                return false;
            };

            SelectedRecipe = RecipeFiles.FirstOrDefault(recipe => recipe.Name == selectedName) 
                ?? RecipeFiles.FirstOrDefault();

            RecipeCount = RecipesView.Cast<RecipeModel>().Count();

            OnPropertyChanged(nameof(RecipesView));
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            SyncToModel();
        }

        partial void OnSelectedRecipeChanged(RecipeModel? value)
        {
            if (value != null)
            {
                SelectedWavelengthIndex = 0;

                LoadWavelengthsToUI();

                SelectedOverEtchText = value.OverEtchEnabled ? "Yes" : "No";
                SelectedAutocalibrationText = value.AutocalibrationEnabled ? "Yes" : "No";
            }
            Log.Information("RecipeViewModel: Recipe changed");
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

        partial void OnSelectedAutocalibrationTextChanged(string value)
        {
            if (SelectedRecipe != null)
            {
                SelectedRecipe.AutocalibrationEnabled = (value == "Yes");
            }
        }
    }
}
