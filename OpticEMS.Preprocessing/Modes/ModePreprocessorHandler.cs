using DynamicExpresso;
using OpticEMS.Contracts.Services.ProcessingModes;
using OpticEMS.Contracts.Services.Recipe;
using Serilog;
using System.Text.RegularExpressions;

namespace OpticEMS.Preprocessing.Modes
{
    public class ModePreprocessorHandler
    {
        private readonly Recipe _recipe;
        private readonly Func<double[], double>? _compiledExpression;

        public ModePreprocessorHandler(Recipe recipe)
        {
            _recipe = recipe;
            if (recipe.ProcessingMode == ProcessingMode.MultiChannel &&
                recipe.MultiSubMode == MultiChannelSubMode.Combined &&
                !string.IsNullOrEmpty(recipe.CombinedExpression))
            {
                _compiledExpression = CompileExpression(recipe.CombinedExpression, recipe.WavelengthNames);
            }

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
                ProcessingMode.MultiChannel => _recipe.MultiSubMode switch
                {
                    MultiChannelSubMode.Combined => new[] { CombinedExpression(data) },
                    _ => data
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

            return Math.Abs(data[1]) == 0 ? 0 : data[0] / data[1];
        }

        private double CombinedExpression(double[] data)
        {
            try
            {
                var result = _compiledExpression(data);

                return result;
            }
            catch (Exception exception)
            {
                return data[0];
            }
        }

        private Func<double[], double> CompileExpression(string expression, List<string> names)
        {
            try
            {
                var interpreter = new Interpreter();
                string finalExpr = expression;

                var sortedNames = names
                    .Select((name, index) => new 
                    { 
                        name, index 
                    });

                foreach (var item in sortedNames)
                {
                    string pattern = $@"\b{Regex.Escape(item.name)}\b";
                    finalExpr = Regex.Replace(finalExpr, pattern, $"data[{item.index}]", RegexOptions.IgnoreCase);
                }

                var result = interpreter.ParseAsDelegate<Func<double[], double>>(finalExpr, "data");
                Log.Information("[PREPROCESSING]: Compiled expression {FinalExpression} for endpoint tracking",
                    finalExpr);

                return result;
            }
            catch
            {
                var interpreter = new Interpreter();
                var name = names.First();
                string finalExpr = expression;

                string pattern = $@"\b{Regex.Escape(name)}\b";

                finalExpr = Regex.Replace(finalExpr, pattern, $"data[0]", RegexOptions.IgnoreCase);
                
                var result = interpreter.ParseAsDelegate<Func<double[], double>>(finalExpr, "data");
                Log.Warning("[PREPROCESSING]: Compiled expression {FinalExpression} for endpoint tracking",
                    finalExpr);

                return result;
            }
        }
    }
}
