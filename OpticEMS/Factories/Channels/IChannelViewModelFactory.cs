using OpticEMS.Contracts.Services.Settings;
using OpticEMS.MVVM.ViewModels.ProcessViewModels;

namespace OpticEMS.Factories.Channels
{
    public interface IChannelViewModelFactory
    {
        ChannelViewModel Create(DeviceInfo configuration);
    }
}
