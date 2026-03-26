using OpticEMS.Contracts.Services.Settings;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Serialization;

namespace OpticEMS.Services.Settings
{
    public class SettingsProvider : ISettingsProvider
    {
        private readonly AppSettings _settings;
        private ObservableCollection<DeviceInfo> _devices;

        public SettingsProvider()
        {
            _settings = AppSettings.Default;
            _devices = LoadDevices();
        }

        private ObservableCollection<DeviceInfo> LoadDevices()
        {
            if (string.IsNullOrWhiteSpace(_settings.DevicesXml))
                return new ObservableCollection<DeviceInfo>();

            try
            {
                using var reader = new StringReader(_settings.DevicesXml);
                var serializer = new XmlSerializer(typeof(ObservableCollection<DeviceInfo>));
                var collection = serializer.Deserialize(reader) as ObservableCollection<DeviceInfo>;
                return collection ?? new ObservableCollection<DeviceInfo>();
            }
            catch (Exception ex)
            {
                return new ObservableCollection<DeviceInfo>();
            }
        }

        private void SaveDevices()
        {
            if (_devices == null || _devices.Count == 0)
            {
                _settings.DevicesXml = "";
                return;
            }

            try
            {
                using var writer = new Utf8StringWriter();
                var serializer = new XmlSerializer(typeof(ObservableCollection<DeviceInfo>));
                serializer.Serialize(writer, _devices);
                _settings.DevicesXml = writer.ToString();
            }
            catch (Exception ex)
            { 

            }
        }

        public IReadOnlyList<DeviceInfo> GetAll() => _devices.AsReadOnly();

        public DeviceInfo? GetByChannelId(int channelId) 
            => _devices.FirstOrDefault(device => device.ChannelId == channelId);

        public void Reload()
        {
            _devices = LoadDevices();
        }

        public bool RemoveByChannelId(int channelId)
        {
            var item = _devices.FirstOrDefault(d => d.ChannelId == channelId);

            if (item == null)
            {
                return false;
            }

            return _devices.Remove(item);
        }

        public void Save()
        {
            SaveDevices();
            _settings.Save();
        }

        public void Upsert(DeviceInfo deviceInfo)
        {
            if (deviceInfo == null)
            {
                return;
            }

            var existing = _devices.FirstOrDefault(d => d.ChannelId == deviceInfo.ChannelId);

            if (existing != null)
            {
                var index = _devices.IndexOf(existing);
                _devices[index] = deviceInfo;
            }
            else
            {
                _devices.Add(deviceInfo);
            }
        }
    }
}
