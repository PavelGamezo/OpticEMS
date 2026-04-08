using CommunityToolkit.Mvvm.ComponentModel;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class ChannelDetailsViewModel : ObservableObject
    {
        public ChannelViewModel Channel { get; set; }

        public ChannelDetailsViewModel(ChannelViewModel channel) 
        {
            Channel = channel;
        }
    }
}
