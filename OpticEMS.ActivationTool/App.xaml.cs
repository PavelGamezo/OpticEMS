using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpticEMS.ActivationTool.MVVM.ViewModels;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace OpticEMS.ActivationTool
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
            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<ActivationSettingsViewModel>();
            services.AddSingleton<LicenseKeyContainerViewModel>();

            // Windows
            services.AddTransient<MainWindow>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            await Host.StartAsync();

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
