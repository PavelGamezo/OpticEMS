namespace OpticEMS.Notifications.Messages
{
    public record ProcessFinishedMessage(int ChannelId, string Report, bool IsForsed);
}
