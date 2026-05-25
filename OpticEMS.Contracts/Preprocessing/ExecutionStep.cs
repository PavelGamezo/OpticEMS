namespace OpticEMS.Contracts.Preprocessing
{
    public class ExecutionStep
    {
        public int ChannelId { get; set; }

        public string NodeId { get; init; } = string.Empty;

        public INodeProcessor Processor { get; init; }

        public List<string> InputNodeIds { get; init; } = new();

        public bool IsSink { get; init; }
    }
}
