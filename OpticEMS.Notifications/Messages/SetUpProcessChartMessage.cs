using System.Windows.Media;

namespace OpticEMS.Notifications.Messages
{
    public record SetUpProcessChartMessage(int ChannelId, List<double> Wavelengths, List<Color> WavelengthColors);
}
