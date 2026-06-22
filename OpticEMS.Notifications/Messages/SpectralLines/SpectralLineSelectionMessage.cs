namespace OpticEMS.Notifications.Messages.SpectralLines
{
    public class SpectralLineSelectionMessage
    {
        public int ChannelId { get; }

        public double Wavelength { get; }

        public string ColorHex { get; }

        public SpectralLineSelectionMessage(int channelId, double wavelength, string colorHex)
        {
            ChannelId = channelId;
            Wavelength = wavelength;
            ColorHex = colorHex;
        }
    }
}
