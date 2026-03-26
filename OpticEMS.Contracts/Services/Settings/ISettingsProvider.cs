namespace OpticEMS.Contracts.Services.Settings
{
    public interface ISettingsProvider
    {
        IReadOnlyList<DeviceInfo> GetAll();

        DeviceInfo? GetByChannelId(int deviceId);

        void Upsert(DeviceInfo deviceInfo);

        bool RemoveByChannelId(int deviceId);

        void Save();

        void Reload();
    }
}
