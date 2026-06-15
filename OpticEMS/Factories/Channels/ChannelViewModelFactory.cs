using Microsoft.Extensions.DependencyInjection;
using OpticEMS.Contracts.Preprocessing;
using OpticEMS.Contracts.Services.Calibration;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Contracts.Services.Dialog;
using OpticEMS.Contracts.Services.Etching;
using OpticEMS.Contracts.Services.Export;
using OpticEMS.Contracts.Services.Mapper;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.MVVM.ViewModels.ProcessViewModels;

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
            IRecipeRepository recipeRepository = _serviceProvider.GetRequiredService<IRecipeRepository>();
            IEtchingProcessService endpointService = _serviceProvider.GetRequiredService<IEtchingProcessService>();
            ISettingsProvider configProvider = _serviceProvider.GetRequiredService<ISettingsProvider>();
            ICalibrationService calibrationService = _serviceProvider.GetRequiredService<ICalibrationService>();
            ISpectralLineRepository spectralLineRepository = _serviceProvider.GetRequiredService<ISpectralLineRepository>();

            var id = configuration.ChannelId;

            return new ChannelViewModel(id,
                wavelengthMapper,
                recipeRepository,
                dialogService,
                endpointService,
                configProvider,
                calibrationService,
                spectralLineRepository);
        }

        public ChannelViewModel CreateDefault() => new ChannelViewModel();
    }
}
