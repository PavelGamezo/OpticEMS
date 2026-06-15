using OpticEMS.Contracts.Services.ProcessingModes;
using OpticEMS.Contracts.Services.Recipe;
using Serilog;

namespace OpticEMS.Preprocessing
{
    public class ModePreprocessingHandler
    {
        private readonly Recipe _recipe;
        private readonly Func<double[], double>? _compiledExpression;

        public ModePreprocessingHandler(Recipe recipe)
        {
            _recipe = recipe;
            Log.Information("[PREPROCESSING]: Processing handler compiled");
        }

        public double[] Process(double[] data)
        {
            if (data == null || data.Length == 0)
            {
                return Array.Empty<double>();
            }

            return _recipe.ProcessingMode switch
            {
                ProcessingMode.SingleChannel => ProcessSimultaneous(data),
                ProcessingMode.DualChannel => _recipe.DualSubMode switch
                {
                    DualChannelSubMode.Ratio => new[] { ProcessRatio(data) },
                    _ => ProcessSimultaneous(data)
                },
                _ => ProcessSimultaneous(data)
            };
        }

        public double[] ProcessSimultaneous(double[] data) => data;

        public double ProcessRatio(double[] data)
        {
            if (data.Length < 2)
            {
                return data.Length > 0 ? data[0] : 0;
            }

            return data[1] == 0 ? 0 : data[0] / data[1];
        }
    }
}
