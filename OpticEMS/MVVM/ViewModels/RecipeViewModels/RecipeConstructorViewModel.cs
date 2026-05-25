using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticEMS.Preprocessing.Graph;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace OpticEMS.MVVM.ViewModels.RecipeViewModels
{
    public class RecipeConstructorViewModel : ObservableObject
    {
        public ObservableCollection<NodeViewModel> Nodes { get; set; } = new();
        public ObservableCollection<ConnectionViewModel> Connections { get; set; } = new();

        private object? _pendingConnectionSource;
        public object? PendingConnectionSource
        {
            get => _pendingConnectionSource;
            set => SetProperty(ref _pendingConnectionSource, value);
        }

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand AddNodeCommand { get; }
        public IRelayCommand DeleteSelectedNodesCommand { get; }

        public RecipeConstructorViewModel()
        {
            ConnectCommand = new RelayCommand<object>(ExecuteConnect);
            DisconnectCommand = new RelayCommand<object>(ExecuteDisconnect);
            AddNodeCommand = new RelayCommand<NodeType>(AddNode);
            DeleteSelectedNodesCommand = new RelayCommand(DeleteSelectedNodes);
        }

        private void ExecuteDisconnect(object? parameter)
        {
            if (parameter is ConnectionViewModel connection) Connections.Remove(connection);
        }

        private void ExecuteConnect(object? parameter)
        {
            PinViewModel? sourcePin = null;
            PinViewModel? targetPin = null;

            if (parameter is ValueTuple<object, object> tuple)
            {
                sourcePin = tuple.Item1 as PinViewModel;
                targetPin = tuple.Item2 as PinViewModel;
            }
            else if (parameter is ITuple genericTuple && genericTuple.Length >= 2)
            {
                sourcePin = genericTuple[0] as PinViewModel;
                targetPin = genericTuple[1] as PinViewModel;
            }

            if (sourcePin != null && targetPin != null && sourcePin.ParentNode != targetPin.ParentNode)
            {
                if (sourcePin.ParentNode.OutputPins.Contains(sourcePin) && targetPin.ParentNode.InputPins.Contains(targetPin))
                {
                    if (WouldCreateCycle(targetPin.ParentNode, sourcePin.ParentNode))
                    {
                        PendingConnectionSource = null;
                        return;
                    }

                    if (!Connections.Any(c => c.Source == sourcePin && c.Target == targetPin))
                    {
                        var existingConnectionOnInput = Connections.FirstOrDefault(c => c.Target == targetPin);
                        if (existingConnectionOnInput != null)
                        {
                            Connections.Remove(existingConnectionOnInput);
                        }

                        Connections.Add(new ConnectionViewModel(sourcePin, targetPin));
                    }
                }
            }
            PendingConnectionSource = null;
        }

        private bool WouldCreateCycle(NodeViewModel startNode, NodeViewModel targetNode)
        {
            var visited = new HashSet<NodeViewModel>();
            return TraverseAndFind(startNode, targetNode, visited);
        }

        private bool TraverseAndFind(NodeViewModel current, NodeViewModel target, HashSet<NodeViewModel> visited)
        {
            if (current == target) return true;
            if (!visited.Add(current)) return false;

            var outputConnections = Connections.Where(c => current.OutputPins.Contains(c.Source));

            foreach (var conn in outputConnections)
            {
                var nextNode = conn.Target.ParentNode;
                if (TraverseAndFind(nextNode, target, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddNode(NodeType type)
        {
            double offsetX = 300 + (Nodes.Count % 5 * 30);
            double offsetY = 200 + (Nodes.Count % 5 * 20);

            var newNode = new NodeViewModel { Type = type, Location = new Point(offsetX, offsetY) };
            InitializePinsForType(newNode, type, default);
            Nodes.Add(newNode);
        }

        private void DeleteSelectedNodes()
        {
            var nodesToDelete = Nodes.Where(n => n.IsSelected).ToList();
            if (!nodesToDelete.Any()) return;

            foreach (var node in nodesToDelete)
            {
                var connectionsToRemove = Connections
                    .Where(c => node.InputPins.Contains(c.Target) || node.OutputPins.Contains(c.Source))
                    .ToList();

                foreach (var connection in connectionsToRemove)
                {
                    Connections.Remove(connection);
                }

                Nodes.Remove(node);
            }
        }

        public void LoadGraphFromJson(string graphJson)
        {
            if (string.IsNullOrWhiteSpace(graphJson)) return;

            try
            {
                Nodes.Clear();
                Connections.Clear();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var rawGraph = JsonSerializer.Deserialize<RawGraph>(graphJson, options);
                if (rawGraph == null) return;

                var nodeMap = new Dictionary<string, NodeViewModel>();

                // 1. Создаем ноды и пины
                foreach (var rawNode in rawGraph.Nodes)
                {
                    NodeType type = MapStringToNodeType(rawNode.Type);
                    double x = 100, y = 100;
                    if (rawNode.Properties.ValueKind != JsonValueKind.Undefined && rawNode.Properties.ValueKind != JsonValueKind.Null)
                    {
                        if (rawNode.Properties.TryGetProperty("x", out var xProp) || rawNode.Properties.TryGetProperty("X", out xProp)) x = xProp.GetDouble();
                        if (rawNode.Properties.TryGetProperty("y", out var yProp) || rawNode.Properties.TryGetProperty("Y", out yProp)) y = yProp.GetDouble();
                    }

                    var nodeVm = new NodeViewModel { Id = rawNode.Id, Type = type, Location = new Point(x, y) };
                    InitializePinsForType(nodeVm, type, rawNode.Properties);
                    RestoreNodeProperties(nodeVm, rawNode.Properties);

                    Nodes.Add(nodeVm);
                    nodeMap[nodeVm.Id] = nodeVm;
                }

                // 2. Создаем связи СРАЗУ. Nodify сам привяжет их к кружочкам по ссылкам объектов!
                foreach (var edge in rawGraph.Edges)
                {
                    if (nodeMap.TryGetValue(edge.Source, out var sourceNode) && nodeMap.TryGetValue(edge.Target, out var targetNode))
                    {
                        PinViewModel? sourcePin = sourceNode.OutputPins.FirstOrDefault();
                        PinViewModel? targetPin = null;

                        if (targetNode.InputPins.Count == 1)
                        {
                            targetPin = targetNode.InputPins[0];
                        }
                        else if (targetNode.InputPins.Count > 1)
                        {
                            var pinA = targetNode.InputPins.FirstOrDefault(p => p.Name == "A");
                            var pinB = targetNode.InputPins.FirstOrDefault(p => p.Name == "B");
                            if (pinA != null && pinB != null)
                            {
                                targetPin = Connections.Any(c => c.Target == pinA) ? pinB : pinA;
                            }
                            else
                            {
                                targetPin = Connections.Any(c => c.Target == targetNode.InputPins[0]) ? targetNode.InputPins[1] : targetNode.InputPins[0];
                            }
                        }

                        if (sourcePin != null && targetPin != null)
                        {
                            Connections.Add(new ConnectionViewModel(sourcePin, targetPin));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка загрузки графа: {ex.Message}");
            }
        }

        private NodeType MapStringToNodeType(string typeStr)
        {
            return typeStr switch
            {
                "Source" => NodeType.SpectralLine,
                "Smoothing" => NodeType.Filter,
                "Derivative" => NodeType.Derivative,
                "Addition" => NodeType.Addition,
                "Subtraction" => NodeType.Subtraction,
                "Division" => NodeType.Division,
                "Multiplication" => NodeType.Multiplication,
                "Sink" => NodeType.Sink,
                _ => NodeType.SpectralLine
            };
        }

        private void RestoreNodeProperties(NodeViewModel node, JsonElement properties)
        {
            if (properties.ValueKind == JsonValueKind.Undefined || properties.ValueKind == JsonValueKind.Null) return;
            if (node.Type == NodeType.Filter)
            {
                if (properties.TryGetProperty("magneticFieldPeriodMs", out var filterProp)) node.FilterPeriod = filterProp.GetDouble();
                if (properties.TryGetProperty("periodsToAverage", out var periodsProp)) node.PeriodToAverage = periodsProp.GetInt32();
            }
            else if (node.Type == NodeType.Derivative && properties.TryGetProperty("derivationTime", out var derivProp)) node.DerivativeTime = derivProp.GetInt32();
            else if (node.Type == NodeType.SpectralLine && properties.TryGetProperty("title", out var titleProp)) node.Title = titleProp.GetString() ?? string.Empty;
        }

        private void InitializePinsForType(NodeViewModel node, NodeType type, JsonElement properties)
        {
            switch (type)
            {
                case NodeType.SpectralLine:
                    if (string.IsNullOrEmpty(node.Title) && properties.ValueKind != JsonValueKind.Undefined && properties.TryGetProperty("title", out var tProp)) node.Title = tProp.GetString() ?? "CH";
                    node.OutputPins.Add(new PinViewModel(node, "Intensity")); break;
                case NodeType.Sink:
                    node.Title = "Endpoint Signal (Sink)"; node.InputPins.Add(new PinViewModel(node, "Final Signal In")); break;
                case NodeType.Filter:
                    node.Title = "Filter (Magnetic)"; node.InputPins.Add(new PinViewModel(node, "Signal In")); node.OutputPins.Add(new PinViewModel(node, "Filtered Out")); break;
                case NodeType.Derivative:
                    node.Title = "Derivative (dI/dt)"; node.InputPins.Add(new PinViewModel(node, "Signal In")); node.OutputPins.Add(new PinViewModel(node, "Result")); break;
                case NodeType.Addition:
                case NodeType.Subtraction:
                case NodeType.Multiplication:
                case NodeType.Division:
                    node.Title = type == NodeType.Addition ? "Add (+)" : type == NodeType.Subtraction ? "Subtract (-)" : type == NodeType.Multiplication ? "Multiply (*)" : "Divide (/)";
                    node.InputPins.Add(new PinViewModel(node, "A")); node.InputPins.Add(new PinViewModel(node, "B")); node.OutputPins.Add(new PinViewModel(node, "Result")); break;
            }
        }

        public string GetGraphJson()
        {
            var graphData = new
            {
                Nodes = this.Nodes.Select(node => new { Id = node.Id, Type = MapNodeTypeToString(node.Type), Properties = node.GetPropertiesAsDictionary() }).ToList(),
                Edges = this.Connections.Select(c => new { Source = c.SourceNodeId, Target = c.TargetNodeId }).ToList()
            };
            return System.Text.Json.JsonSerializer.Serialize(graphData);
        }

        private string MapNodeTypeToString(NodeType type)
        {
            return type switch
            {
                NodeType.SpectralLine => "Source",
                NodeType.Filter => "Smoothing",
                NodeType.Derivative => "Derivative",
                NodeType.Addition => "Addition",
                NodeType.Subtraction => "Subtraction",
                NodeType.Division => "Division",
                NodeType.Multiplication => "Multiplication",
                NodeType.Sink => "Sink",
                _ => "Unknown"
            };
        }
    }
}
