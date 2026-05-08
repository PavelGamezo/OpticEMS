using System.Text.RegularExpressions;

namespace OpticEMS.Services.Validators
{
    public class ExpressionValidator : IExpressionValidator
    {
        public ValidationResult Validate(string expression, List<string> channelNames)
        {
            if (string.IsNullOrEmpty(expression))
            {
                return new ValidationResult(false, "Empty expression");
            }

            if (channelNames == null || channelNames.Count == 0)
            {
                return new ValidationResult(false, "Empty channel names list");
            }

            if (!IsBalancedBrackets(expression))
            {
                return new ValidationResult(false, "Invalid expression");
            }

            var sortedNames = channelNames
                .Select((name, index) => new { name = name.Trim(), index })
                .Where(x => !string.IsNullOrEmpty(x.name))
                .OrderByDescending(x => x.name.Length);

            string testExpr = expression;

            foreach (var item in sortedNames)
            {
                string pattern = $@"\b{Regex.Escape(item.name)}\b";
                testExpr = Regex.Replace(testExpr, pattern, $"100", RegexOptions.IgnoreCase);
            }

            string cleaned = Regex.Replace(testExpr, @"[+\-*/().0-9\s]", "").Trim();

            if (!string.IsNullOrEmpty(cleaned))
            {
                return new ValidationResult(false, "Invalid expression");
            }

            try
            {
                var table = new System.Data.DataTable();
                var testResult = table.Compute(testExpr, null);

                if (testResult == null || testResult == DBNull.Value)
                {
                    return new ValidationResult(false, "Invalid expression");
                }

                return new ValidationResult(true, "Expression correct");
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, "Invalid expression");
            }
        }

        private static bool IsBalancedBrackets(string expr)
        {
            int count = 0;

            foreach (char c in expr)
            {
                if (c == '(') count++;
                if (c == ')') count--;
                if (count < 0) return false;
            }
            return count == 0;
        }
    }
}
