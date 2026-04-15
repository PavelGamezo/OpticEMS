namespace OpticEMS.Contracts.Services.Settings
{
    public interface ISettingsProvider
    {
        int MaxAllowedChannels { get; set; }

        string EntrySecret { get; set; }

        IReadOnlyList<DeviceInfo> GetAll();

        DeviceInfo? GetByChannelId(int deviceId);

        void Upsert(DeviceInfo deviceInfo);

        bool RemoveByChannelId(int deviceId);

        void Save();

        void Reload();
    }
}
