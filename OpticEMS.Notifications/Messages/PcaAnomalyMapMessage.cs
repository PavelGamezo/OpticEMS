namespace OpticEMS.Notifications.Messages
{
    public record PcaAnomalyMapMessage(int ChannelId, List<(int, int)> Ranges);
}
