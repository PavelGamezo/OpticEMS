using OpticEMS.Contracts.Factories;
using OpticEMS.Contracts.Preprocessing;
using System.Text.Json;

namespace OpticEMS.Preprocessing.Graph
{
    public class GraphCompiler : IGraphCompiler
    {
        private readonly INodeProcessorFactory _processorFactory;

        public GraphCompiler(INodeProcessorFactory processorFactory)
        {
            _processorFactory = processorFactory;
        }

        public List<ExecutionStep> Compile(string graphJson)
        {
            if (string.IsNullOrWhiteSpace(graphJson))
            {
                return null;
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var rawGraph = JsonSerializer.Deserialize<RawGraph>(graphJson, options);

            if (rawGraph == null || rawGraph.Nodes.Count == 0)
            {
                throw new InvalidOperationException("Не удалось распарсить граф или в нем нет узлов.");
            }

            var executionPipeline = new List<ExecutionStep>();

            var inDegree = rawGraph.Nodes.ToDictionary(n => n.Id, _ => 0);

            var adjacencyList = rawGraph.Nodes.ToDictionary(n => n.Id, _ => new List<string>());

            foreach (var edge in rawGraph.Edges)
            {
                if (inDegree.ContainsKey(edge.Target) && adjacencyList.ContainsKey(edge.Source))
                {
                    adjacencyList[edge.Source].Add(edge.Target);
                    inDegree[edge.Target]++;
                }
            }

            var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));

            var channelId = 0;

            while (queue.Count > 0)
            {
                var currentNodeId = queue.Dequeue();
                var rawNode = rawGraph.Nodes.First(n => n.Id == currentNodeId);

                var inputIds = rawGraph.Edges
                    .Where(e => e.Target == currentNodeId)
                    .Select(e => e.Source)
                    .ToList();

                var processor = _processorFactory.CreateProcessor(rawNode.Type, rawNode.Properties);

                var step = new ExecutionStep
                {
                    ChannelId = channelId++,
                    NodeId = currentNodeId,
                    Processor = processor,
                    InputNodeIds = inputIds,
                    IsSink = rawNode.Type.Equals("Sink", StringComparison.OrdinalIgnoreCase)
                };

                executionPipeline.Add(step);

                foreach (var neighborId in adjacencyList[currentNodeId])
                {
                    inDegree[neighborId]--;

                    if (inDegree[neighborId] == 0)
                    {
                        queue.Enqueue(neighborId);
                    }
                }
            }

            if (executionPipeline.Count != rawGraph.Nodes.Count)
            {
                throw new InvalidOperationException(
                    "Критическая ошибка компиляции рецепта: в графе обнаружена циклическая зависимость (петля) или оборванные связи!");
            }

            if (!executionPipeline.Any(s => s.IsSink))
            {
                throw new InvalidOperationException("Ошибка рецепта: в графе отсутствует финальный узел 'Sink' для вывода сигнала.");
            }

            return executionPipeline;
        }
    }
}
