using System.Text.Json;

namespace OpticEMS.Preprocessing.Graph
{
    public class RawNode
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public JsonElement Properties { get; set; }
    }
}
