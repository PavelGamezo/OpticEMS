namespace OpticEMS.Notifications.Messages
{
    public record LiveSpectrumDataMessage(int ChannelId, double[] Wavelengths, double[] Intensities);
}
