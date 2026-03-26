using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Factories.Channels;
using OpticEMS.Services.Dialogs;
using OpticEMS.Services.Settings;
using OpticEMS.Services.Spectrometers;
using OpticEMS.MVVM.Models;
using System.Collections.ObjectModel;
using OpticEMS.MVVM.ViewModels.RecipeViewModels;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class ProcessViewModel : ObservableObject
    {
        private readonly IChannelViewModelFactory _channelViewModelFactory;
        private readonly ISpectrometerService _spectrometerService;
        private readonly IDialogService _dialogService;
        private RecipeViewModel _recipeViewModel;

        public ObservableCollection<ChannelViewModel> Channels { get; } = new();

        [ObservableProperty]
        private ChannelViewModel? _selectedChannel;

        public ProcessViewModel(IChannelViewModelFactory channelViewModelFactory,
            ISpectrometerService spectrometerService,
            IDialogService dialogService,
            RecipeViewModel recipeViewModel)
        {
            _channelViewModelFactory = channelViewModelFactory;
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
            var targetChannel = Channels.FirstOrDefault(c => c.ChannelId == recipe.Channel); 

            if (targetChannel != null)
            {
                targetChannel.ApplyRecipe(recipe);
            } 
            else 
            { 
                _dialogService.ShowInformation($"Channel {recipe.Channel} not found");
            }
        }

        private void InitializeChannels()
        {
            var count = _spectrometerService.GetConnectedSpectrometersCount();
            var appConfig = AppSettings.Default.Devices;

            foreach (var config in appConfig)
            {
                var channel = _channelViewModelFactory.Create(config);
                Channels.Add(channel);
            }

            SelectedChannel = Channels.FirstOrDefault();
        }
    }
}
