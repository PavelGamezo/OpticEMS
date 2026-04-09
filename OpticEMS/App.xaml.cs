using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Data.Database.Context;
using OpticEMS.Data.Repositories;
using OpticEMS.Data.Services;
using OpticEMS.Factories.Channels;
using OpticEMS.MVVM.Models;
using OpticEMS.MVVM.ViewModels.ProcessViewModels;
using OpticEMS.MVVM.ViewModels.RecipeViewModels;
using OpticEMS.MVVM.ViewModels.SettingsViewModels;
using OpticEMS.Services.Calibration;
using OpticEMS.Services.Dialogs;
using OpticEMS.Services.Etching;
using OpticEMS.Services.Export;
using OpticEMS.Services.Files;
using OpticEMS.Services.Spectrometers;
using OpticEMS.Services.Times;
using OpticEMS.Services.Windows;
using OpticEMS.ViewModels;
using System.IO;
using System.Windows;

namespace OpticEMS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IHost Host { get; set; }

        public App()
        {
            Host = Microsoft.Extensions.Hosting.Host
                .CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<RecipeModel>();

            // Services
            services.AddSingleton<ISettingsProvider, OpticEMS.Services.Settings.SettingsProvider>(); // Singleton
            services.AddScoped<ISpectrometerService, SpectrometerService>();
            services.AddScoped<IWindowService, WindowService>();
            services.AddScoped<ITimeService, TimeService>();
            services.AddScoped<ICalibrationService, CalibrationService>();
            services.AddScoped<IRecipeFileManager, RecipeFileManager>();
            services.AddScoped<IWavelengthMapper, WavelengthMapper>();
            services.AddScoped<IDialogService, DialogService>();
            services.AddScoped<IExportManager, ExportManager>();
            services.AddTransient<IEtchingProcessService, EtchingProcessService>();

            // Repositories
            services.AddScoped<ISpectralLineRepository, SpectralLineRepository>();

            // Databases
            services.AddScoped<AppDbContext>();

            // Orchestrators

            // Factories
            services.AddScoped<IChannelViewModelFactory, ChannelViewModelFactory>();

            services.AddTransient<Func<int, SpectralLinesCatalogViewModel>>(provider =>
            {
                return channelId => new SpectralLinesCatalogViewModel(
                    channelId,
                    provider.GetRequiredService<ISpectralLineRepository>(),
                    provider.GetRequiredService<IDialogService>()
                );
            });

            // ViewModels
            services.AddSingleton<ProcessViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<ChamberSettingsViewModel>();
            services.AddSingleton<CalibrationSettingsViewModel>();
            services.AddSingleton<RecipeViewModel>();
            services.AddSingleton<RenameDialogViewModel>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SpectrumChartViewModel>();
            services.AddSingleton<ProcessChartViewModel>();

            // Windows
            services.AddTransient<MainWindow>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            await Host.StartAsync();

            using (var db = new AppDbContext())
            {
                await db.Database.EnsureCreatedAsync();

                await SpectralLinesSeeder.SeedFromCsvAsync(db);
            }

            var mainWindow = Host.Services.GetRequiredService<MainWindow>();

            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await Host.StopAsync();

            base.OnExit(e);
        }
    }

}
