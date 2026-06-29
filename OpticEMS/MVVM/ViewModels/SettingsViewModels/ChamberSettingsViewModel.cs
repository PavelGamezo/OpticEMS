using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpticEMS.Common.Enums;
using OpticEMS.Contracts.Services.Dialog;
using OpticEMS.Contracts.Services.Settings;
using OpticEMS.Devices.Devices.Avantes;
using OpticEMS.Devices.Devices.Solar;
using OpticEMS.Devices.Devices.Yixis;
using OpticEMS.MVVM.Models;
using OpticEMS.Notifications.Messages;
using OpticEMS.Services.Settings;
using OpticEMS.Services.Spectrometers;
using Serilog;
using System.Collections.ObjectModel;
using System.Text;

namespace OpticEMS.MVVM.ViewModels.SettingsViewModels
{
    public partial class ChamberSettingsViewModel : ObservableObject
    {
        private readonly ISpectrometerService _spectrometerService;
        private readonly IDialogService _dialogService;

        private List<string> _cachedAvailableSpectrometers;

        public ObservableCollection<ChannelModel> ChannelSettings { get; } = new();

        public IEnumerable<SpectrometerType> SpectrometerTypes { get; } =
            Enum.GetValues(typeof(SpectrometerType)).Cast<SpectrometerType>();

        public ChamberSettingsViewModel(
            ISpectrometerService spectrometerService,
            IDialogService dialogService)
        {
            try
            {
                _spectrometerService = spectrometerService;
                _dialogService = dialogService;
                GetSetupSettings();

                RegisterMessages();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[ChamberSettingsViewModel]: Fatal error during initialization.");
            }
        }

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<ChannelsUpdatedMessage>(this, (recipient, message) =>
            {
                GetSetupSettings();
            });
        }

        private List<string> GetAvailableSpectrometers()
        {
            if (_cachedAvailableSpectrometers != null)
                return _cachedAvailableSpectrometers;

            var specs = new List<string>();
            int count = _spectrometerService.GetConnectedSpectrometersCount();

            Log.Information("[ChamberSettingsViewModel]: Found {Count} connected devices.", count);

            for (int i = 0; i < count; i++)
            {
                var serial = _spectrometerService.GetSerialNumber(i);
                if (!string.IsNullOrEmpty(serial))
                    specs.Add(serial);
            }

            if (!specs.Any(s => s.Contains("VIRTUAL")))
                specs.Add("VIRTUAL-SPEC-001");

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
                    var model = new ChannelModel
                    {
                        ChannelId = dev.ChannelId,
                        AvailableSpectrometers = new List<string>(allSpecs),
                        SelectedSpectrometer = !string.IsNullOrEmpty(dev.Name) ? dev.Name : "VIRTUAL-SPEC-001",

                        SelectedSpectrometerType = dev.DeviceType switch
                        {
                            DeviceType.Solar => SpectrometerType.Solar,
                            DeviceType.Avantes => SpectrometerType.Avantes,
                            DeviceType.Yixist => SpectrometerType.Yixist,
                            DeviceType.VirtualSpec => SpectrometerType.VirtualSpec,
                            _ => SpectrometerType.VirtualSpec
                        },

                        CoefficientA = $"{dev.CoefA}",
                        CoefficientB = $"{dev.CoefB}",
                        CoefficientC = $"{dev.CoefC}",
                        CoefficientD = $"{dev.CoefD}",

                        TrimLeft = dev.TrimLeft,
                        TrimRight = dev.TrimRight,

                        DeviceIpAddress = dev.DeviceIp ?? string.Empty
                    };

                    ChannelSettings.Add(model);
                }
            }
            else
            {
                int count = Math.Max(1, _spectrometerService.GetConnectedSpectrometersCount());
                for (int i = 0; i < count; i++)
                    AddChamber();
            }
        }

        [RelayCommand]
        private async Task ConnectYixistByIp(ChannelModel channel)
        {
            if (string.IsNullOrWhiteSpace(channel.DeviceIpAddress))
                return;

            string ip = channel.DeviceIpAddress.Trim();

            channel.IsYixistConnecting = true;
            channel.YixistConnectionStatus = "Connecting...";

            try
            {
                string? sn = await Task.Run(() => TryGetYixistSN(ip));

                if (sn == null)
                {
                    channel.YixistConnectionStatus = "❌ Not found";
                    Log.Warning("[ChamberSettingsViewModel]: Yixist not found at {IP}", ip);
                    return;
                }

                if (!channel.AvailableSpectrometers.Contains(sn))
                {
                    channel.AvailableSpectrometers = new List<string>(channel.AvailableSpectrometers) { sn };

                    if (_cachedAvailableSpectrometers != null && !_cachedAvailableSpectrometers.Contains(sn))
                        _cachedAvailableSpectrometers.Add(sn);
                }

                channel.SelectedSpectrometer = sn;
                channel.YixistConnectionStatus = $"✓ {sn}";

                Log.Information("[ChamberSettingsViewModel]: Yixist connected at {IP}, SN={SN}", ip, sn);
            }
            catch (Exception ex)
            {
                channel.YixistConnectionStatus = "❌ Error";
                Log.Error(ex, "[ChamberSettingsViewModel]: Error connecting Yixist at {IP}", ip);
            }
            finally
            {
                channel.IsYixistConnecting = false;
            }
        }

        private static string? TryGetYixistSN(string ip, int port = 8080)
        {
            uint handle = 0;
            try
            {
                handle = YixistCCD.SPConnectTCP(ip, port);
                if (handle == 0) return null;

                byte[] buf = new byte[256];
                if (!YixistCCD.SPGetSN(handle, buf)) return null;

                string sn = Encoding.ASCII.GetString(buf).Replace('\r', '\0');
                int idx = sn.IndexOf('\0');
                sn = idx >= 0 ? sn[..idx] : sn;

                return string.IsNullOrWhiteSpace(sn) ? $"Yixist-{ip}" : sn;
            }
            catch { return null; }
            finally
            {
                if (handle != 0) YixistCCD.SPClose(handle);
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

                    item.DeviceType = item.SelectedSpectrometerType switch
                    {
                        SpectrometerType.Solar => DeviceType.Solar,
                        SpectrometerType.Avantes => DeviceType.Avantes,
                        SpectrometerType.Yixist => DeviceType.Yixist,
                        SpectrometerType.VirtualSpec => DeviceType.VirtualSpec,
                        _ => DeviceType.VirtualSpec
                    };

                    DeviceInfo device;

                    if (item.SelectedSpectrometerType == SpectrometerType.VirtualSpec ||
                        item.SelectedSpectrometer == "VIRTUAL-SPEC-001")
                    {
                        device = new DeviceInfo(
                            "VIRTUAL-SPEC-001", 2048, 0, channelId, DeviceType.VirtualSpec,
                            (int)item.TrimLeft, (int)item.TrimRight,
                            -1.029096988621741E-08, -5.332134891649228E-06,
                            0.3790476108105803, 344.5635979788548, 0)
                        { ExposureTime = 1, ScansNum = 1, Equalizer = 1 };
                    }
                    else if (item.DeviceType == DeviceType.Avantes)
                    {
                        var av = new Avantes();
                        device = av.DeviceInfo;
                        device.DeviceType = DeviceType.Avantes;
                        device.ChannelId = channelId;
                        device.TrimLeft = (int)item.TrimLeft;
                        device.TrimRight = (int)item.TrimRight;
                        device.ExposureTime = 1; device.ScansNum = 1; device.Equalizer = 1;
                    }
                    else if (item.DeviceType == DeviceType.Yixist)
                    {
                        if (string.IsNullOrWhiteSpace(item.DeviceIpAddress))
                        {
                            _dialogService.ShowError($"Chamber {channelId + 1}: Yixist IP is not set.");
                            return;
                        }

                        var yixist = new Yixist(item.DeviceIpAddress.Trim());
                        device = yixist.DeviceInfo;
                        device.DeviceType = DeviceType.Yixist;
                        device.ChannelId = channelId;
                        device.TrimLeft = (int)item.TrimLeft;
                        device.TrimRight = (int)item.TrimRight;
                        device.ExposureTime = 1; device.ScansNum = 1; device.Equalizer = 1;
                    }
                    else
                    {
                        var solar = new Solar(channelId, serial);
                        device = solar.DeviceInfo;
                        device.DeviceType = DeviceType.Solar;
                        device.ChannelId = channelId;
                        device.TrimLeft = (int)item.TrimLeft;
                        device.TrimRight = (int)item.TrimRight;
                        device.ExposureTime = 1; device.ScansNum = 1; device.Equalizer = 1;
                    }

                    devices.Add(device);
                }

                AppSettings.Default.Devices = devices;
                AppSettings.Default.Save();

                WeakReferenceMessenger.Default.Send(new ChannelsUpdatedMessage());

                Log.Information("[ChamberSettingsViewModel]: Settings saved. Channels={Count}", channelId + 1);
                _dialogService.ShowInformation("Settings saved successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ChamberSettingsViewModel]: Failed to save settings");
                _dialogService.ShowError($"Failed to save settings: {ex.Message}");
            }
        }

        [RelayCommand]
        private void AddChamber()
        {
            ChannelSettings.Add(new ChannelModel
            {
                ChannelId = ChannelSettings.Count + 1,
                AvailableSpectrometers = GetAvailableSpectrometers(),
                SelectedSpectrometer = "VIRTUAL-SPEC-001"
            });
        }

        [RelayCommand]
        private void RemoveChamber(ChannelModel channel)
        {
            if (!ChannelSettings.Contains(channel)) return;

            if (_dialogService.AskWarningQuestion("Would you like to delete this chamber?"))
            {
                ChannelSettings.Remove(channel);
                for (int i = 0; i < ChannelSettings.Count; i++)
                    ChannelSettings[i].ChannelId = i + 1;
            }
        }
    }
}