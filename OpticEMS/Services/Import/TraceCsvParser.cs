using OpticEMS.Contracts.Services.Import;
using Serilog;
using System.Globalization;
using System.IO;

namespace OpticEMS.Services.Import
{
    public class TraceCsvParser
    {
        private static readonly char[] Separators = [';'];
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static ImportData Parse(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Trace file not found.", filePath);
            }

            var lines = File.ReadAllLines(filePath);

            Log.Information("[TRACE_PARSER]: Parsing {File} ({Lines} lines)", filePath, lines.Length);

            var meta = ParseMetadata(lines);
            var (seriesNames, dataRows) = ParseDataSection(lines);
            var series = BuildSeries(seriesNames, dataRows);

            return new ImportData
            {
                StartTime = meta.startTime,
                EndTime = meta.endTime,
                OverEtchStartTime = meta.overEtchStart,
                OverEtchEndTime = meta.overEtchEnd,
                RecipeName = meta.recipe,
                ChannelName = meta.channel,
                Series = series
            };
        }

        private static (DateTime startTime, DateTime endTime,
                         DateTime overEtchStart, DateTime overEtchEnd,
                         string recipe, string channel)
            ParseMetadata(string[] lines)
        {
            DateTime startTime = DateTime.MinValue;
            DateTime endTime = DateTime.MinValue;
            DateTime overEtchStart = DateTime.MinValue;
            DateTime overEtchEnd = DateTime.MinValue;
            string recipe = string.Empty;
            string channel = string.Empty;

            foreach (var line in lines.Take(10))
            {
                if (TryParseMetaLine(line, "Start Process Time:", out var val))
                    DateTime.TryParse(val, out startTime);

                else if (TryParseMetaLine(line, "End Process Time:", out val))
                    DateTime.TryParse(val, out endTime);

                else if (TryParseMetaLine(line, "Overetching Time:", out val))
                    ParseOveretchRange(val, out overEtchStart, out overEtchEnd);

                else if (TryParseMetaLine(line, "Recipe:", out val))
                    recipe = val;

                else if (TryParseMetaLine(line, "Channel:", out val))
                    channel = val;
            }

            return (startTime, endTime, overEtchStart, overEtchEnd, recipe, channel);
        }

        private static bool TryParseMetaLine(string line, string key, out string value)
        {
            if (line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                value = line[key.Length..].Trim();
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static void ParseOveretchRange(string val,
            out DateTime start, out DateTime end)
        {
            start = DateTime.MinValue;
            end = DateTime.MinValue;

            var parts = val.Split("to", 2);
            if (parts.Length == 2)
            {
                DateTime.TryParse(parts[0].Trim(), out start);
                DateTime.TryParse(parts[1].Trim(), out end);
            }
        }

        private static (string[] seriesNames, IEnumerable<string> dataRows)
            ParseDataSection(string[] lines)
        {
            int separatorIndex = Array.FindIndex(lines, l => l.StartsWith("===="));
            if (separatorIndex < 0)
            {
                throw new FormatException("Export file separator '====' not found.");
            }

            int headerIndex = separatorIndex + 1;
            if (headerIndex >= lines.Length)
            {
                throw new FormatException("Column header line not found after separator.");
            }

            var headers = lines[headerIndex].Split(Separators, StringSplitOptions.None);

            var seriesNames = headers.Skip(1).ToArray();

            var dataRows = lines
                .Skip(headerIndex + 1)
                .TakeWhile(l => !l.StartsWith("====") && l != "End of export")
                .Where(l => !string.IsNullOrWhiteSpace(l));

            return (seriesNames, dataRows);
        }

        private static IReadOnlyList<TraceSeries> BuildSeries(
            string[] seriesNames, IEnumerable<string> dataRows)
        {
            var pointLists = seriesNames
                .Select(_ => new List<(double, double)>())
                .ToArray();

            int parsedRows = 0;
            int skippedRows = 0;

            foreach (var row in dataRows)
            {
                var cols = row.Split(Separators, StringSplitOptions.None);

                if (cols.Length < 2)
                {
                    skippedRows++;
                    continue;
                }

                if (!double.TryParse(cols[0], NumberStyles.Float, Culture, out double timeSeconds))
                {
                    skippedRows++;
                    continue;
                }

                for (int i = 0; i < seriesNames.Length; i++)
                {
                    int colIndex = i + 1;

                    if (colIndex >= cols.Length)
                    {
                        break;
                    }

                    string raw = cols[colIndex].Trim();

                    if (raw.Equals("NaN", StringComparison.OrdinalIgnoreCase) ||
                        !double.TryParse(raw, NumberStyles.Float, Culture, out double intensity))
                    {
                        continue;
                    }

                    pointLists[i].Add((timeSeconds, intensity));
                }

                parsedRows++;
            }

            Log.Information("[TRACE_PARSER]: Parsed {Rows} rows, skipped {Skipped}",
                parsedRows, skippedRows);

            return seriesNames
                .Select((name, i) => new TraceSeries(name, pointLists[i]))
                .ToList();
        }
    }
}
