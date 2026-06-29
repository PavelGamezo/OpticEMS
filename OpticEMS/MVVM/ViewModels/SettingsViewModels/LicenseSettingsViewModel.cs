using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using OpticEMS.Contracts.Services.Dialog;
using OpticEMS.MVVM.View.Settings;
using Serilog;
using System.Reflection;

namespace OpticEMS.MVVM.ViewModels.SettingsViewModels
{
    public partial class LicenseSettingsViewModel : ObservableObject
    {
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _company;

        [ObservableProperty]
        private string _product;

        [ObservableProperty]
        private string _copyright;

        [ObservableProperty]
        private string _description;

        [ObservableProperty]
        private string _projectVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        [ObservableProperty]
        private string _edition = "System configuration: Commercial";

        public LicenseSettingsViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;

            var attributes = Assembly.GetExecutingAssembly()
                .GetCustomAttributes();

            foreach (var attribute in attributes)
            {
                if (attribute is AssemblyTitleAttribute)
                {
                    Title = (attribute as AssemblyTitleAttribute).Title;
                }

                if (attribute is AssemblyCompanyAttribute)
                {
                    Company = (attribute as AssemblyCompanyAttribute).Company;
                }

                if (attribute is AssemblyProductAttribute)
                {
                    Product = (attribute as AssemblyProductAttribute).Product;
                }

                if (attribute is AssemblyCopyrightAttribute)
                {
                    Copyright = (attribute as AssemblyCopyrightAttribute).Copyright;
                }

                if (attribute is AssemblyDescriptionAttribute)
                {
                    Description = (attribute as AssemblyDescriptionAttribute).Description;
                }
            }
        }

        [RelayCommand]
        private async Task OpenUpdateDialog()
        {
            if (_dialogService.AskUpdate())
            {
                Log.Information("[UpdateViewModel]: Update accepted.");
            }
            else
            {
                Log.Information("[UpdateViewModel]: Update denied.");
            }
        }
    }
}
