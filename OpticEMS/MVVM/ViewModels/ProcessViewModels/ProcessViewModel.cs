using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Factories.Channels;
using System.Collections.ObjectModel;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class ProcessViewModel : ObservableObject
    {
        private readonly IChannelViewModelFactory _channelViewModelFactory;
        private readonly ISettingsProvider _settingsProvider;

        public ObservableCollection<ChannelViewModel> Channels { get; } = new();

        [ObservableProperty]
        private ChannelViewModel? _selectedChannel;

        public ProcessViewModel(
            IChannelViewModelFactory channelViewModelFactory,
            ISettingsProvider settingsProvider)
        {
            _channelViewModelFactory = channelViewModelFactory;
            _settingsProvider = settingsProvider;

            InitializeChannels();
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
