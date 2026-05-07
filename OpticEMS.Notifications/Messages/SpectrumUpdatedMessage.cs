namespace OpticEMS.Notifications.Messages
{
    public record SpectrumUpdatedMessage(int ChannelId, double[] Intensities, double[] Wavelengths);
}
