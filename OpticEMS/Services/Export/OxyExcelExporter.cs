using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using OxyPlot;
using OxyPlot.Series;
using Serilog;
using System.IO;

namespace OpticEMS.Services.Export
{
    public class OxyExcelExporter : IExporter
    {
        private readonly DateTime _startTime;
        private readonly DateTime _endTime;
        private readonly DateTime _overEtchStartTime;
        private readonly DateTime _overEtchEndTime;
        private readonly string _recipeName;
        private readonly string _channelName;

        public OxyExcelExporter(
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
            try
            {
                using (var package = new ExcelPackage())
                {
                    if (model is PlotModel plotModel)
                    {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("OES Data");
                        int row = 1;

                        WriteMetadataHeader(worksheet, ref row);
                        row += 2;

                        var dataSeriesList = plotModel.Series
                            .OfType<DataPointSeries>()
                            .Where(s => s.IsVisible && s.Points.Count > 0)
                            .ToList();

                        if (dataSeriesList.Count == 0)
                        {
                            worksheet.Cells[row, 1].Value = "No visible data series found in PlotModel.";
                            package.SaveAs(stream);
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

                        int tableHeaderRow = row;
                        worksheet.Cells[row, 1].Value = "Time (s)";
                        worksheet.Cells[row, 1].Style.Font.Bold = true;

                        for (int i = 0; i < dataSeriesList.Count; i++)
                        {
                            var series = dataSeriesList[i];
                            string seriesHeader = !string.IsNullOrEmpty(series.Title) ? series.Title : $"Series_{i}";
                            worksheet.Cells[row, i + 2].Value = seriesHeader;
                            worksheet.Cells[row, i + 2].Style.Font.Bold = true;
                        }
                        row++;

                        int dataStartRow = row;

                        var seriesDictionaries = dataSeriesList
                            .Select(s => s.Points
                                .Select(p => new {
                                    Seconds = Math.Round((OxyPlot.Axes.DateTimeAxis.ToDateTime(p.X) - graphAbsoluteStart).TotalSeconds, 3),
                                    Value = p.Y
                                })
                                .GroupBy(p => p.Seconds)
                                .ToDictionary(g => g.Key, g => g.First().Value))
                            .ToList();

                        foreach (double timeInSeconds in allTimestampsInSeconds)
                        {
                            worksheet.Cells[row, 1].Value = timeInSeconds;
                            worksheet.Cells[row, 1].Style.Numberformat.Format = "0.000";

                            for (int i = 0; i < seriesDictionaries.Count; i++)
                            {
                                if (seriesDictionaries[i].TryGetValue(timeInSeconds, out double intensity))
                                {
                                    worksheet.Cells[row, i + 2].Value = intensity;
                                    worksheet.Cells[row, i + 2].Style.Numberformat.Format = "0.00";
                                }
                            }
                            row++;
                        }

                        int dataEndRow = row - 1;

                        string chartTitle = plotModel.Title ?? "Process Chart";
                        AddExcelChart(worksheet, chartTitle, tableHeaderRow, dataStartRow, dataEndRow, dataSeriesList.Count);

                        worksheet.Cells[1, 1, dataEndRow, dataSeriesList.Count + 1].AutoFitColumns(12, 22);
                        package.SaveAs(stream);
                    }
                }

                Log.Information("[EXPORT]: Excel Export completed successfully.");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[EXPORT]: Error during Excel export process from PlotModel");
                throw;
            }
        }

        private void WriteMetadataHeader(ExcelWorksheet ws, ref int row)
        {
            int WriteLine(int currentRow, string label, string val)
            {
                ws.Cells[currentRow, 1].Value = label;
                ws.Cells[currentRow, 1].Style.Font.Bold = true;
                ws.Cells[currentRow, 2].Value = val;
                return ++currentRow;
            }

            row = WriteLine(row, "Start Process Time:", _startTime.ToString("dd.MM.yyyy HH:mm:ss"));
            row = WriteLine(row, "End Process Time:", _endTime.ToString("dd.MM.yyyy HH:mm:ss"));
            row = WriteLine(row, "Endpoint Detection Time:", _overEtchStartTime.ToString("dd.MM.yyyy HH:mm:ss"));

            ws.Cells[row, 1].Value = "Overetching Time:";
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 2].Value = _overEtchStartTime.ToString("dd.MM.yyyy HH:mm:ss");
            ws.Cells[row, 3].Value = "to";
            ws.Cells[row++, 4].Value = _overEtchEndTime.ToString("dd.MM.yyyy HH:mm:ss");

            row = WriteLine(row, "Export Time:", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
            row = WriteLine(row, "Recipe:", _recipeName ?? "N/A");
            row = WriteLine(row, "Channel:", _channelName ?? "N/A");
        }

        private void AddExcelChart(ExcelWorksheet ws, string title, int headerRow, int dataStartRow, int lastDataRow, int seriesCount)
        {
            var chart = ws.Drawings.AddChart("DataChart", eChartType.XYScatterLinesNoMarkers);
            chart.Title.Text = title;

            int chartColumnPosition = seriesCount + 3;
            chart.SetPosition(headerRow - 1, 0, chartColumnPosition, 0);
            chart.SetSize(1000, 500);

            chart.XAxis.Title.Text = "Time (seconds)";
            chart.YAxis.Title.Text = "Trend (a.u.)";

            var xRange = ws.Cells[dataStartRow, 1, lastDataRow, 1];

            for (int i = 0; i < seriesCount; i++)
            {
                int targetColumn = i + 2;

                var yRange = ws.Cells[dataStartRow, targetColumn, lastDataRow, targetColumn];

                var chartSeries = chart.Series.Add(yRange, xRange);

                chartSeries.HeaderAddress = ws.Cells[headerRow, targetColumn];
            }
        }
    }
}
