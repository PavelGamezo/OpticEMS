namespace OpticEMS.Contracts.ProcessingModes
{
    public enum ProcessingMode
    {
        SingleChannel = 0,
        DualChannel = 1,
        MultiChannel = 2
    }

    public enum DualChannelSubMode
    {
        Simultaneous = 0,
        Ratio = 1
    }

    public enum MultiChannelSubMode
    {
        Simultaneous = 0,
        Ratio = 1,
        Combined = 2
    }
}

