namespace OpticEMS.Notifications.Messages
{
    public record SpectrumUpdatedMessage(int ChannelId, uint[] Intensities, double[] Wavelengths);
}
