namespace OpticEMS.Notifications.Messages
{
    public class PeakModeChangedMessage
    {
        public int ChannelId { get; }
        public bool IsEnabled { get; }

        public PeakModeChangedMessage(int channelId, bool isEnabled)
        {
            ChannelId = channelId;
            IsEnabled = isEnabled;
        }
    }
}
