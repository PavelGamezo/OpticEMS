using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Devices.Devices.Avantes;
using OpticEMS.Devices.Devices.Solar;
using OpticEMS.MVVM.ViewModels.SettingsViewModels;
using OpticEMS.Services.Dialogs;
using OpticEMS.Services.Settings;
using OpticEMS.Services.Spectrometers;
using OpticEMS.MVVM.Models;
using System.Collections.ObjectModel;

namespace OpticEMS.MVVM.ViewModels.SettingsViewModels
{
    public partial class ChamberSettingsViewModel : ObservableObject
    {
        private readonly ISpectrometerService _spectrometerService;
        private readonly IDialogService _dialogService;

        private List<string> _cachedAvailableSpectrometers;

        public ObservableCollection<ChannelModel> ChannelSettings { get; } = new();

        public ChamberSettingsViewModel(ISpectrometerService spectrometerService,
            IDialogService dialogService)
        {
            _spectrometerService = spectrometerService;
            _dialogService = dialogService;

            GetSetupSettings();
        }

        private List<string> GetAvailableSpectrometers()
        {
            if (_cachedAvailableSpectrometers != null) return _cachedAvailableSpectrometers;

            var specs = new List<string>();

            int count = _spectrometerService.GetConnectedSpectrometersCount();

            for (int i = 0; i < count; i++)
            {
                var serial = _spectrometerService.GetSerialNumber(i);
                if (!string.IsNullOrEmpty(serial)) specs.Add(serial);
            }

            if (!specs.Any(s => s.Contains("VIRTUAL")))
            {
                specs.Add("VIRTUAL-SPEC-001");
            }

            _cachedAvailableSpectrometers = specs;
            return specs;
        }

        private void GetSetupSettings()
        {
            var allSpecs = GetAvailableSpectrometers();
            ChannelSettings.Clear();

            var devices = AppSettings.Default.Devices;

            if (devices != null && devices.Any())
            {
                foreach (var dev in devices)
                {
                    ChannelSettings.Add(new ChannelModel
                    {
                        ChannelId = dev.ChannelId,
                        AvailableSpectrometers = new List<string>(allSpecs),
                        SelectedSpectrometer = !string.IsNullOrEmpty(dev.Name) ? dev.Name : "VIRTUAL-SPEC-001"
                    });
                }
            }
            else
            {
                var count = Math.Max(1, _spectrometerService.GetConnectedSpectrometersCount());
                for (int i = 0; i < count; i++)
                {
                    AddChamber();
                }
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            try
            {
                var devices = AppSettings.Default.Devices;

                devices.Clear();
                int channelId = -1;

                foreach (var item in ChannelSettings)
                {
                    channelId++;
                    var serial = item.SelectedSpectrometer ?? "VIRTUAL-SPEC-001";

                    item.DeviceType = SpectrometerTypeDetector.Detect(serial);

                    DeviceInfo device;

                    if (item.SelectedSpectrometer == "VIRTUAL-SPEC-001")
                    {
                        device = new DeviceInfo(
                            "VIRTUAL-SPEC-001",
                            2048,0,channelId,
                            DeviceType.VirtualSpec,
                            10,
                            0,
                            -1.029096988621741E-08,
                            -5.332134891649228E-06,
                            0.3790476108105803,
                            344.5635979788548,
                            0);

                        devices.Add(device);
                    }
                    else if (item.DeviceType == DeviceType.Avantes)
                    {
                        var av = new Avantes();
                        device = av.DeviceInfo;
                        device.DeviceType = DeviceType.Avantes;
                        device.ChannelId = channelId;

                        devices.Add(device);
                    }
                    else
                    {
                        var solar = new Solar(serial);
                        device = solar.DeviceInfo;
                        device.DeviceType = DeviceType.Solar;
                        device.ChannelId = channelId;

                        devices.Add(device);
                    }
                }

                AppSettings.Default.Devices = devices;
                AppSettings.Default.Save();

                _dialogService.ShowInformation("Settings saved successfully.");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Failed to save settings: {ex.Message}");
            }
        }

        [RelayCommand]
        private void AddChamber()
        {
            ChannelSettings.Add(new ChannelModel
            {
                ChannelId = ChannelSettings.Count,
                AvailableSpectrometers = GetAvailableSpectrometers(),
                SelectedSpectrometer = "VirtualSpec"
            });
        }

        [RelayCommand]
        private void RemoveChamber(ChannelModel channel)
        {
            if (ChannelSettings.Contains(channel))
            {
                if (_dialogService.AskWarningQuestion("Would you like to delete this chamber?"))
                {
                    ChannelSettings.Remove(channel);

                    for (int i = 0; i < ChannelSettings.Count; i++)
                    {
                        ChannelSettings[i].ChannelId = i;
                    }
                }
            }
        }
    }
}
