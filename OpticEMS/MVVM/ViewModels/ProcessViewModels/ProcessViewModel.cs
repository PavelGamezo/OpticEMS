using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Contracts.Services.Recipe;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Factories.Channels;
using OpticEMS.MVVM.ViewModels.RecipeViewModels;
using OpticEMS.Notifications.Messages;
using Serilog;
using System.Collections.ObjectModel;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class ProcessViewModel : ObservableObject, IDisposable
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
            try
            {
                _channelViewModelFactory = channelViewModelFactory;
                _settingsProvider = settingsProvider;

                InitializeChannels();
                _recipeViewModel = recipeViewModel;
                SubscribeToRecipeChanges();

                RegistedMessages();

                Log.Information("[ProcessViewModel]: Process control compiled");
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, "[ProcessViewModel]: Fatal error during process control starting up");
                throw;
            }
        }

        private void SubscribeToRecipeChanges()
        {
            _recipeViewModel.ApplyRecipeRequested = OnApplyRecipeRequested;
        }

        private void OnApplyRecipeRequested(Recipe recipe)
        {
            var targetChannel = Channels.FirstOrDefault(c => c.ChannelId == recipe.RecipeId - 1);
            
            if (targetChannel is null)
            {
                Log.Warning("[ProcessViewModel]: Recipe apply requested but no channel is selected. Recipe={Name}",
                    recipe.Name);

                return;
            }

            targetChannel.ApplyRecipe(recipe);
        }

        private void RegistedMessages()
        {
            WeakReferenceMessenger.Default.Register<ChannelsUpdatedMessage>(this, (recipient, message) =>
            {
                Log.Information("[ProcessViewModel]: Channels updated message received — reinitializing");

                foreach (var channel in Channels)
                {
                    channel.Dispose();
                }

                Channels.Clear();

                InitializeChannels();
            });
        }

        private void InitializeChannels()
        {
            _settingsProvider.Reload();

            var allDevices = _settingsProvider.GetAll();
            int limit = _settingsProvider.MaxAllowedChannels;
            var limitedDevices = allDevices.Take(limit).ToList();

            Log.Debug("[ProcessViewModel]: Initializing {Count}/{Limit} channels",
                limitedDevices.Count, limit);

            foreach (var config in limitedDevices)
            {
                var channel = _channelViewModelFactory.Create(config);
                Channels.Add(channel);
            }

            if (Channels.Count == 0 && limit > 0)
            {
                Log.Warning("[ProcessViewModel]: No devices configured — creating default virtual channel");

                var channel = _channelViewModelFactory.CreateDefault();
                Channels.Add(channel);
            }

            SelectedChannel = Channels.FirstOrDefault();

            Log.Information("[ProcessViewModel]: Channels initialized. Count={Count}", Channels.Count);
        }

        public void Dispose()
        {
            Log.Debug("[ProcessViewModel]: Disposing {Count} channels", Channels.Count);

            foreach (var channel in Channels)
            {
                channel.Dispose();
            }
        }
    }
}
