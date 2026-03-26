using Microsoft.Extensions.DependencyInjection;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.MVVM.ViewModels.ProcessViewModels;
using OpticEMS.Services.Calibration;
using OpticEMS.Services.Dialogs;
using OpticEMS.Services.Etching;
using OpticEMS.Services.Export;

namespace OpticEMS.Factories.Channels
{
    public class ChannelViewModelFactory : IChannelViewModelFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ChannelViewModelFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ChannelViewModel Create(DeviceInfo configuration)
        {
            IWavelengthMapper wavelengthMapper = _serviceProvider.GetRequiredService<IWavelengthMapper>();
            IDialogService dialogService = _serviceProvider.GetRequiredService<IDialogService>();
            IEtchingProcessService endpointService = _serviceProvider.GetRequiredService<IEtchingProcessService>();
            ISettingsProvider configProvider = _serviceProvider.GetRequiredService<ISettingsProvider>();
            IExportManager exportManager = _serviceProvider.GetRequiredService<IExportManager>();

            var id = configuration.ChannelId;

            return new ChannelViewModel(
                id,
                wavelengthMapper,
                dialogService,
                endpointService,
                configProvider,
                exportManager);
        }
    }
}
