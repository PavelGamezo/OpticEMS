using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Data.Database.Context;
using OpticEMS.Data.Repositories;
using OpticEMS.Data.Services;
using OpticEMS.Factories.Channels;
using OpticEMS.License.Common;
using OpticEMS.License.Handlers;
using OpticEMS.MVVM.Models;
using OpticEMS.MVVM.View.Activation;
using OpticEMS.MVVM.View.Windows;
using OpticEMS.MVVM.ViewModels;
using OpticEMS.MVVM.ViewModels.Activation;
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
using Serilog;
using System.IO;
using System.Reflection;
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
            services.AddSingleton<ActivationViewModel>();
            services.AddSingleton<PasswordDialogViewModel>();

            // Windows
            services.AddTransient<MainWindow>();
            services.AddTransient<PasswordWindow>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            ConnectLogger();

            CheckLicense(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            Log.Information("Closing current application");

            await Host.StopAsync();

            Log.Information("=== OpticEMS completion ===");

            base.OnExit(e);
        }

        private void ConnectLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("Loggers/log-.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("=== OpticEMS started ===");

            SetupExceptionHandling();
        }

        private void SetupExceptionHandling()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                Log.Fatal(e.Exception, "Unhandled UI dispatcher exception: {Message}", e.Exception.Message);

                e.Handled = false;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                Log.Fatal(exception, "Unhandled AppDomain exception. Terminating: {IsTerminating}", e.IsTerminating);
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log.Error(e.Exception, "Unobserved task exception");
                e.SetObserved();
            };
        }

        private async void CheckLicense(StartupEventArgs e)
        {
            OpticEMSLicense license = null;
            LicenseStatus status;
            byte[] certPubicKeyData;
            string message;
            Log.Information("Started license checking...");

            var assembly = Assembly.GetExecutingAssembly();
            using (var mem = new MemoryStream())
            {
                assembly.GetManifestResourceStream(assembly.GetName().Name + ".LicenseVerify.cer")?.CopyTo(mem);

                certPubicKeyData = mem.ToArray();
            }

            if (File.Exists(Environment.CurrentDirectory + "\\license.lic"))
            {
                license = (OpticEMSLicense)LicenseHandler.ParseLicenseFromBase64String(
                    File.ReadAllText("license.lic"),
                    certPubicKeyData,
                    out status,
                    out message);

                Log.Information("File license exists, checking license status...");
            }
            else
            {
                status = LicenseStatus.Invalid;
                message = "Your copy of this application is not activated";

                Log.Information("File license.lic not exists");
            }

            switch (status)
            {
                case LicenseStatus.Valid:

                    Log.Information("License status is \"Valid\". Starting current application.");
                    Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
                    await Host.StartAsync();

                    var settings = Host.Services.GetRequiredService<ISettingsProvider>();

                    settings.MaxAllowedChannels = license.ChannelCount;
                    settings.EntrySecret = license.EntrySecret;

                    using (var db = new AppDbContext())
                    {
                        Log.Information("SQLite creating...");

                        await db.Database.EnsureCreatedAsync();
                        await SpectralLinesSeeder.SeedFromCsvAsync(db);

                        Log.Information("SQLite created...");
                    }

                    Log.Information("Creating MainWindow view...");
                    var mainWindow = Host.Services.GetRequiredService<MainWindow>();
                    mainWindow.Show();

                    base.OnStartup(e);
                    break;

                case LicenseStatus.Expired:

                    Log.Information("License status is \"Expired\". Starting application activation.");
                    File.Delete("license.lic");
                    Log.Information("File \"license.lic\" was deleted from current directory.");
                    MessageBox.Show(message, string.Empty, MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShowActivationWindow(certPubicKeyData);
                    break;

                default:

                    Log.Information("Unhandled license status. Starting application activation.");
                    MessageBox.Show(message, string.Empty, MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShowActivationWindow(certPubicKeyData);

                    break;

            }
        }

        private void ShowActivationWindow(byte[] certPubicKeyData)
        {
            Log.Information("Started ActivationWindow creation...");

            var window = new ActivationWindow(null);

            IWindowService windowService = new WindowService();

            var viewModel = new ActivationViewModel(windowService);
            viewModel.ConveyCertificate(certPubicKeyData);

            window.DataContext = viewModel;

            Log.Information("Ended ActivationWindow creation. Showing ActivationWindow view");
            window.ShowDialog();
        }
    }
}
