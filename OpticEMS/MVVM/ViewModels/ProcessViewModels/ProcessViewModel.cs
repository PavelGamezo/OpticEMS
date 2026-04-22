using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Factories.Channels;
using OpticEMS.Services.Dialogs;
using OpticEMS.Services.Settings;
using OpticEMS.Services.Spectrometers;
using System.Collections.ObjectModel;
using OpticEMS.MVVM.ViewModels.RecipeViewModels;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.MVVM.Models.Recipe;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class ProcessViewModel : ObservableObject
    {
        private readonly IChannelViewModelFactory _channelViewModelFactory;
        private readonly ISettingsProvider _settingsProvider;
        private readonly ISpectrometerService _spectrometerService;
        private readonly IDialogService _dialogService;
        private RecipeViewModel _recipeViewModel;

        public ObservableCollection<ChannelViewModel> Channels { get; } = new();

        [ObservableProperty]
        private ChannelViewModel? _selectedChannel;

        public ProcessViewModel(IChannelViewModelFactory channelViewModelFactory,
            ISettingsProvider settingsProvider,
            ISpectrometerService spectrometerService,
            IDialogService dialogService,
            RecipeViewModel recipeViewModel)
        {
            _channelViewModelFactory = channelViewModelFactory;
            _settingsProvider = settingsProvider;
            _spectrometerService = spectrometerService;
            _recipeViewModel = recipeViewModel;
            _dialogService = dialogService;

            InitializeChannels();
            SubscribeToRecipeChanges();
        }

        private void SubscribeToRecipeChanges()
        {
            _recipeViewModel.ApplyRecipeRequested = OnApplyRecipeRequested;
        }

        private void OnApplyRecipeRequested(RecipeModel recipe)
        {
            var targetChannel = Channels.FirstOrDefault(c => c.ChannelId == recipe.Channel - 1); 

            if (targetChannel != null)
            {
                targetChannel.ApplyRecipe(recipe);
            } 
            else 
            { 
                _dialogService.ShowError($"Channel {recipe.Channel} not found");
            }
        }

        private void InitializeChannels()
        {
            var allDevices = _settingsProvider.GetAll();

            int limit = _settingsProvider.MaxAllowedChannels;

            var limitedDevices = allDevices.Take(limit).ToList();

            foreach (var config in limitedDevices)
            {
                var channel = _channelViewModelFactory.Create(config);
                Channels.Add(channel);
            }

            if (Channels.Count == 0 && limit > 0)
            {
                var channel = _channelViewModelFactory.CreateDefault();
                Channels.Add(channel);
            }

            SelectedChannel = Channels.FirstOrDefault();
        }
    }
}
