using OpticEMS.Services.Etching;

namespace OpticEMS.Notifications.Messages
{
    public record DrawWindowBoundsMessage(
        int ChannelId, 
        List<WindowBounds> WindowBounds, 
        List<WindowBounds> ConfirmedWindowsIn,
        List<WindowBounds> ConfirmedWindowsOut);
}
