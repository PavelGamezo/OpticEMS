using System.Diagnostics;

namespace OpticEMS.Notifications.Messages
{
    public record UpdateChartAreasMessage(int ChannelId, string Status, Stopwatch Stopwatch);
}
