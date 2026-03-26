using OpticEMS.Contracts.Services.Settings;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace OpticEMS.Services.Settings
{
    public class AppSettings : ApplicationSettingsBase
    {
        private static readonly Lazy<AppSettings> _lazy =
        new Lazy<AppSettings>(() => (AppSettings)Synchronized(new AppSettings()));

        public static AppSettings Default => _lazy.Value;

        [UserScopedSetting]
        [SettingsSerializeAs(SettingsSerializeAs.String)]
        [DefaultSettingValue("")]
        public string DevicesXml
        {
            get => (string)this["DevicesXml"];
            set => this["DevicesXml"] = value;
        }

        [Browsable(false)]
        public ObservableCollection<DeviceInfo> Devices
        {
            get
            {
                if (string.IsNullOrWhiteSpace(DevicesXml))
                    return new ObservableCollection<DeviceInfo>();

                try
                {
                    using var reader = new StringReader(DevicesXml);
                    var serializer = new XmlSerializer(typeof(ObservableCollection<DeviceInfo>));
                    var collection = serializer.Deserialize(reader) as ObservableCollection<DeviceInfo>;
                    return collection ?? new ObservableCollection<DeviceInfo>();
                }
                catch (Exception ex)
                {
                    return new ObservableCollection<DeviceInfo>();
                }
            }
            set
            {
                if (value == null || value.Count == 0)
                {
                    DevicesXml = "";
                    return;
                }

                try
                {
                    using var writer = new Utf8StringWriter();
                    var serializer = new XmlSerializer(typeof(ObservableCollection<DeviceInfo>));
                    serializer.Serialize(writer, value);
                    DevicesXml = writer.ToString();
                }
                catch (Exception ex)
                {
                }
            }
        }
    }

    public class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
