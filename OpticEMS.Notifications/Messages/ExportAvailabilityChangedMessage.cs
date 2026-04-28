namespace OpticEMS.Notifications.Messages
{
    public record ExportAvailabilityChangedMessage(int ChannelId, bool CanExport);
}
