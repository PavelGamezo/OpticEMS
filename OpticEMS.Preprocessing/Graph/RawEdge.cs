namespace OpticEMS.Preprocessing.Graph
{
    public class RawEdge
    {
        public string SourceNodeId { get; set; } = string.Empty;
        public string SourcePinName { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string TargetPinName { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
    }
}
