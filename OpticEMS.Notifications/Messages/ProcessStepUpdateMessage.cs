namespace OpticEMS.Notifications.Messages
{
    public record ProcessStepUpdateMessage(
        int ChannelId,
        string Status,
        double CurrentTime,
        uint[] IntensitiesSnapshot);
}
