using OxyPlot;
using OxyPlot.Series;
using Serilog;
using System.IO;
using System.Text;

namespace OpticEMS.Services.Export
{
    public class OxyCsvExporter : IExporter
    {
        private readonly DateTime _startTime;
        private readonly DateTime _endTime;
        private readonly DateTime _overEtchStartTime;
        private readonly DateTime _overEtchEndTime;
        private readonly string _recipeName;
        private readonly string _channelName;

        public OxyCsvExporter(
            DateTime startTime, DateTime endTime, DateTime overEtchStartTime,
            DateTime OverEtchEndTime, string recipeName, string channelName)
        {
            _startTime = startTime;
            _endTime = endTime;
            _overEtchStartTime = overEtchStartTime;
            _overEtchEndTime = OverEtchEndTime;
            _recipeName = recipeName;
            _channelName = channelName;

        }

        public void Export(IPlotModel model, Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            try
            {
                using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true);
                var culture = System.Globalization.CultureInfo.InvariantCulture;

                writer.WriteLine($"Start Process Time: {_startTime}");
                writer.WriteLine($"End Process Time: {_endTime}");
                writer.WriteLine($"Overetching Time: {_overEtchStartTime} to {_overEtchEndTime}");
                writer.WriteLine($"Export Date: {DateTime.Now}");
                writer.WriteLine($"Recipe: {_recipeName ?? "N/A"}");
                writer.WriteLine($"Channel: {_channelName}");
                writer.WriteLine(new string('=', 80));

                if (model is PlotModel plotModel)
                {
                    var dataSeriesList = plotModel.Series
                            .OfType<DataPointSeries>()
                            .Where(s => s.IsVisible && s.Points.Count > 0)
                            .ToList();

                    if (dataSeriesList.Count == 0)
                    {
                        return;
                    }

                    var allPointsWithDates = dataSeriesList
                        .SelectMany(s => s.Points)
                        .Select(p => new {
                            ParsedDate = OxyPlot.Axes.DateTimeAxis.ToDateTime(p.X),
                            RawPoint = p
                        })
                        .ToList();

                    DateTime graphAbsoluteStart = allPointsWithDates.Min(p => p.ParsedDate);

                    var allTimestampsInSeconds = allPointsWithDates
                        .Select(p => Math.Round((p.ParsedDate - graphAbsoluteStart).TotalSeconds, 3))
                        .Distinct()
                        .OrderBy(t => t)
                        .ToList();

                    writer.Write("Time (s)");
                    foreach (var series in dataSeriesList)
                    {
                        string seriesName = !string.IsNullOrEmpty(series.Title) ?
                            series.Title :
                            $"Series_{dataSeriesList.IndexOf(series)}";

                        writer.Write($";{seriesName}");
                    }

                    writer.WriteLine();

                    var seriesDictionaries = dataSeriesList
                        .Select(s => s.Points
                            .Select(p => new {
                                Seconds = Math.Round((OxyPlot.Axes.DateTimeAxis.ToDateTime(p.X) - graphAbsoluteStart).TotalSeconds, 3),
                                Value = p.Y
                            })
                            .GroupBy(p => p.Seconds)
                            .ToDictionary(g => g.Key, g => g.First().Value))
                        .ToList();

                    foreach (double time in allTimestampsInSeconds)
                    {
                        writer.Write(time.ToString("F3", culture));

                        foreach (var dict in seriesDictionaries)
                        {
                            if (dict.TryGetValue(time, out double intensity))
                            {
                                writer.Write($";{intensity.ToString(culture)}");
                            }
                            else
                            {
                                writer.Write(";NaN");
                            }
                        }
                        writer.WriteLine();
                    }

                    writer.WriteLine(new string('=', 80));
                    writer.WriteLine("End of export");
                    writer.Flush();
                }

                Log.Information("[EXPORT]: Export directly from PlotModel completed successfully");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[EXPORT]: Error exporting data from PlotModel");
                throw;
            }
        }
    }
}
