using OpticEMS.Contracts.Factories;
using OpticEMS.Contracts.Preprocessing;
using OpticEMS.Preprocessing.Operations.Arithmetic;
using OpticEMS.Preprocessing.Operations.Averaging;
using OpticEMS.Preprocessing.Operations.Derivation;
using System.Text.Json;

namespace OpticEMS.Preprocessing.Factories
{
    public class NodeProcessorFactory : INodeProcessorFactory
    {
        public INodeProcessor CreateProcessor(string nodeType, JsonElement jsonProperties)
        {
            return nodeType.ToLower().Trim() switch
            {
                "derivative" => new DerivativeCalculator(
                    derivationTime: jsonProperties.GetProperty("derivationTime").GetInt32()
                ),

                "smoothing" => new MagneticFieldSmoother(
                    magneticFieldPeriodMs: jsonProperties.GetProperty("magneticFieldPeriodMs").GetDouble(),
                    periodsToAverage: jsonProperties.GetProperty("periodsToAverage").GetInt32()
                ),

                "source" => new PassThroughProcessor(),
                "sink" => new PassThroughProcessor(),

                "addition" => new AdditionProcessor(),
                "subtraction" => new SubtractionProcessor(),
                "multiplication" => new MultiplicationProcessor(),
                "division" => new DivisionProcessor(),
            };
        }

        public class PassThroughProcessor : INodeProcessor
        {
            public double Process(double[] inputs, double currentTimeMs)
            {
                return inputs[0];
            }

            public void Reset() { }
        }
    }
}
