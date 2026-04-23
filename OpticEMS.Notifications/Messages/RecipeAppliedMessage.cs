using System.Windows.Media;

namespace OpticEMS.Notifications.Messages
{
    public record RecipeAppliedMessage(int ChannelId, List<double> Wavelengths, List<Color> WavelengthColors);
}
