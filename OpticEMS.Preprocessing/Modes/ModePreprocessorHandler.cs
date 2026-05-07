using OpticEMS.Contracts.ProcessingModes;
using OpticEMS.Contracts.Services.Recipe;

namespace OpticEMS.Preprocessing.Modes
{
    public class ModePreprocessorHandler
    {
        private readonly Recipe _recipe;

        public ModePreprocessorHandler(Recipe recipe)
        {
            _recipe = recipe;
        }

        public double[] Process(uint[] data)
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
                ProcessingMode.MultiChannel => _recipe.MultiSubMode switch
                {
                    MultiChannelSubMode.Combined => new[] { ProcessCombined(data) },
                    _ => ProcessSimultaneous(data)
                },
                _ => ProcessSimultaneous(data)
            };
        }

        public double[] ProcessSimultaneous(uint[] data)
        {
            var result = new double[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = data[i];
            }

            return result;
        }

        public double ProcessRatio(uint[] data)
        {
            uint numerator = data[0];
            uint denominator = data[1];

            var result = numerator / Math.Max(denominator, 1.0);

            return result;
        }

        public double ProcessCombined(uint[] data)
        {
            return EvaluateExpression(_recipe.CombinedExpression, data);
        }

        private double EvaluateExpression(string expression, uint[] data)
        {
            try
            {
                string expr = expression.Replace(" ", "").ToUpper();

                for (int i = 0; i < _recipe.WavelengthNames.Count; i++)
                {
                    var name = _recipe.WavelengthNames[i]?.Trim().ToUpper();
                    if (!string.IsNullOrEmpty(name))
                    {
                        expr = expr.Replace(name, data[i].ToString());
                    }
                }

                var table = new System.Data.DataTable();
                var result = table.Compute(expr, null);

                return Convert.ToDouble(result);
            }
            catch (Exception ex)
            {
                return data.Length > 0 ? data[0] : 0;
            }
        }
    }
}
