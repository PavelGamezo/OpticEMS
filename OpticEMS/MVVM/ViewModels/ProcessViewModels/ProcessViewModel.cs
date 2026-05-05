using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Contracts.Services.Recipe;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Factories.Channels;
using OpticEMS.MVVM.ViewModels.RecipeViewModels;
using System.Collections.ObjectModel;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class ProcessViewModel : ObservableObject
    {
        private readonly IChannelViewModelFactory _channelViewModelFactory;
        private readonly ISettingsProvider _settingsProvider;
        private readonly RecipeViewModel _recipeViewModel;

        public ObservableCollection<ChannelViewModel> Channels { get; } = new();

        [ObservableProperty]
        private ChannelViewModel? _selectedChannel;

        public ProcessViewModel(
            IChannelViewModelFactory channelViewModelFactory,
            ISettingsProvider settingsProvider,
            RecipeViewModel recipeViewModel)
        {
            _channelViewModelFactory = channelViewModelFactory;
            _settingsProvider = settingsProvider;

            InitializeChannels();
            _recipeViewModel = recipeViewModel;
            SubscribeToRecipeChanges();
        }

        private void SubscribeToRecipeChanges()
        {
            _recipeViewModel.ApplyRecipeRequested = OnApplyRecipeRequested;
        }

        private void OnApplyRecipeRequested(Recipe recipe)
        {
            var targetChannel = Channels.FirstOrDefault(c => c.ChannelId == recipe.DatabaseId - 1);

            if (targetChannel != null)
            {
                targetChannel.ApplyRecipe(recipe);
            }
            else
            {
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
