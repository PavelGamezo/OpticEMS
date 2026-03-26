namespace OpticEMS.Notifications.Messages
{
    public enum State
    {
        Idle, Stabilizing, Monitoring, OverEtching, Finished
    }

    public enum Trigger
    {
        Start, InWindowReached, EndpointDetected, OverEtchFinished, Stop
    }
}
