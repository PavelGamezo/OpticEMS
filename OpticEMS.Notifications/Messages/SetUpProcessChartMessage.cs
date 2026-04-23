using System.Windows.Media;

namespace OpticEMS.Notifications.Messages
{
    public record SetUpProcessChartMessage(List<double> Wavelengths, List<Color> WavelengthColors);
}
