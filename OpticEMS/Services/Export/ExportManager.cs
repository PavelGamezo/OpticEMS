using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using OpticEMS.Contracts.Services.Export;
using Serilog;
using System.IO;
using System.Text;

namespace OpticEMS.Services.Export
{
    public class ExportManager : IExportManager
    {
        public void ExportAsTextFormat(string path, DateTime startTime, DateTime endTime, DateTime overEtchStartTime,
            DateTime overEtchEndTime, string recipeName, string channelName, List<double> wavelengths, List<TimePoint> points)
        {
            try
            {
                using var writer = new StreamWriter(path, false, Encoding.UTF8);
                var culture = System.Globalization.CultureInfo.InvariantCulture;

                writer.WriteLine($"Start Process Time: {startTime}");
                writer.WriteLine($"End Process Time: {endTime}");
                writer.WriteLine($"Overetching Time: {overEtchStartTime} to {overEtchEndTime}");
                writer.WriteLine($"Export Date: {DateTime.Now}");
                writer.WriteLine($"Recipe: {recipeName ?? "N/A"}");
                writer.WriteLine($"Channel: {channelName}");

                WriteTextSection(writer, "1. RAW DATA (After Averaging + Magnetic Field)", wavelengths, points, points => points.Trend);

                WriteTextSection(writer, "2. PREPROCESSED DATA (Smoothing / Derivative)", wavelengths, points, points => points.Preprocessed);

                WriteTextSection(writer, "3. FINAL PROCESSED DATA (Ratio / Combined)", wavelengths, points, points => points.Processed);

                writer.WriteLine(new string('=', 80));
                writer.WriteLine("End of export");

                Log.Information("[EXPORT]: Export completed successfully");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[EXPORT]: Error during export process");
                throw;
            }
        }

        public void ExportAsXLS(string path, DateTime startTime, DateTime endTime, DateTime overEtchStartTime,
            DateTime overEtchEndTime, string recipeName, string channelName, List<double> wavelengths, List<TimePoint> points)
        {
            try
            {
                var excelPackage = GenerateExcelPackage(startTime, endTime, overEtchStartTime,
                overEtchEndTime, recipeName, channelName, wavelengths, points);

                if (excelPackage != null)
                {
                    File.WriteAllBytes(path, excelPackage);
                }

                Log.Information("[EXPORT]: Export completed successfully");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[EXPORT]: Error during export process");
                throw;
            }
        }

        private byte[]? GenerateExcelPackage(DateTime startTime, DateTime endTime, DateTime overEtchStartTime,
            DateTime overEtchEndTime, string recipeName, string channelName, 
            List<double> wavelengths, List<TimePoint> points)
        {
            byte[] result;

            using (var package = new ExcelPackage())
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                ExcelWorksheet worksheets = package.Workbook.Worksheets.Add($"Data");
                int row = 1;

                worksheets.Cells[row, 1].Value = "Start Process Time: ";
                worksheets.Cells[row, 1].Style.Font.Bold = true;
                worksheets.Cells[row++, 2].Value = startTime.ToString("dd.MM.yyyy HH:mm:ss");

                worksheets.Cells[row, 1].Value = "End Process Time: ";
                worksheets.Cells[row, 1].Style.Font.Bold = true;
                worksheets.Cells[row++, 2].Value = endTime.ToString("dd.MM.yyyy HH:mm:ss");

                worksheets.Cells[row, 1].Value = "Endpoint Detection Time: ";
                worksheets.Cells[row, 1].Style.Font.Bold = true;
                worksheets.Cells[row++, 2].Value = overEtchStartTime.ToString("dd.MM.yyyy HH:mm:ss");

                worksheets.Cells[row, 1].Value = "Overetching Time: ";
                worksheets.Cells[row, 1].Style.Font.Bold = true;
                worksheets.Cells[row, 2].Value = overEtchStartTime.ToString("dd.MM.yyyy HH:mm:ss");
                worksheets.Cells[row, 3].Value = "to";
                worksheets.Cells[row++, 4].Value = overEtchEndTime.ToString("dd.MM.yyyy HH:mm:ss");

                worksheets.Cells[row, 1].Value = "Export Time:";
                worksheets.Cells[row, 1].Style.Font.Bold = true;
                worksheets.Cells[row++, 2].Value = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");

                worksheets.Cells[row, 1].Value = "Recipe:";
                worksheets.Cells[row, 1].Style.Font.Bold = true;
                worksheets.Cells[row++, 2].Value = recipeName;

                worksheets.Cells[row, 1].Value = "Channel:";
                worksheets.Cells[row, 1].Style.Font.Bold = true;
                worksheets.Cells[row++, 2].Value = channelName;

                worksheets.Cells[row, 1].Value = "Time (s)";
                worksheets.Cells[row, 1].Style.Font.Bold = true;

                row += 2;

                int rawStart = WriteDataSection(worksheets, ref row, "1. RAW Data (After Averaging + Magnetic Field)", wavelengths, points, points => points.Trend);
                AddChart(worksheets, "RawChart", "RAW Data", rawStart, row - 1, wavelengths);

                row += 4;

                int preprocStart = WriteDataSection(worksheets, ref row, "2. Preprocessed Data (Smoothing / Derivative)", wavelengths, points, points => points.Preprocessed);
                AddChart(worksheets, "PreprocessedChart", "Preprocessed Data", preprocStart, row - 1, wavelengths);

                row += 4;

                int processedStart = WriteDataSection(worksheets, ref row, "3. Final Processed Data (Ratio / Combined)", wavelengths, points, points => points.Processed);
                AddChart(worksheets, "ProcessedChart", "Final Processed Data", processedStart, row - 1, wavelengths);

                worksheets.Cells[worksheets.Dimension.Address].AutoFitColumns(12, 20);
                result = package.GetAsByteArray();
            }

            return result;
        }

        private void WriteTextSection(StreamWriter writer, string title,
            List<double> wavelengths, List<TimePoint> points, Func<TimePoint, double[]> selector)
        {
            if (points.Count == 0)
            {
                writer.WriteLine($"\n{title}: No data");
                return;
            }

            writer.WriteLine($"\n{title}");
            writer.WriteLine(new string('-', 60));

            writer.Write("Time (s)");

            foreach (var wl in wavelengths)
            {
                writer.Write($";Intensity {wl:F2} nm");
            }

            writer.WriteLine();

            foreach (var point in points)
            {
                double[] intensities = selector(point) ?? Array.Empty<double>();
                writer.Write(point.TimeSeconds.ToString("F3"));

                foreach (var intensity in intensities)
                {
                    writer.Write($";{intensity}");
                }

                writer.WriteLine();
            }
        }

        private void AddChart(ExcelWorksheet ws, string chartName, string title,
            int dataStartRow, int lastDataRow, List<double> wavelengths)
        {
            if (dataStartRow >= lastDataRow)
            {
                return;
            }

            var chart = ws.Drawings.AddChart(chartName, eChartType.XYScatterLinesNoMarkers);
            chart.Title.Text = title;
            chart.SetPosition(lastDataRow + 2, 0, 1, 0);
            chart.SetSize(950, 420);

            chart.XAxis.Title.Text = "Time (seconds)";
            chart.YAxis.Title.Text = "Intensity";

            for (int i = 0; i < wavelengths.Count; i++)
            {
                var xRange = ws.Cells[dataStartRow, 1, lastDataRow, 1];
                var yRange = ws.Cells[dataStartRow, i + 2, lastDataRow, i + 2];

                var series = chart.Series.Add(yRange, xRange);
                series.Header = $"{wavelengths[i]:F2} nm";
            }
        }

        private int WriteDataSection(ExcelWorksheet ws, ref int row, string title,
            List<double> wavelengths, List<TimePoint> points, Func<TimePoint, double[]> selector)
        {
            if (points.Count == 0)
            {
                return row;
            }

            ws.Cells[row++, 1].Value = title;
            ws.Cells[row - 1, 1].Style.Font.Bold = true;
            ws.Cells[row - 1, 1].Style.Font.Size = 13;

            ws.Cells[row, 1].Value = "Time (s)";
            ws.Cells[row, 1].Style.Font.Bold = true;

            for (int i = 0; i < wavelengths.Count; i++)
            {
                ws.Cells[row, i + 2].Value = $"Wavelengths: {wavelengths[i]:F2} nm";
                ws.Cells[row, i + 2].Style.Font.Bold = true;
            }
            row++;

            int dataStartRow = row;

            foreach (var point in points)
            {
                double[] intensities = selector(point) ?? Array.Empty<double>();

                ws.Cells[row, 1].Value = point.TimeSeconds;
                ws.Cells[row, 1].Style.Numberformat.Format = "0.000";

                for (int i = 0; i < intensities.Length && i < wavelengths.Count; i++)
                {
                    ws.Cells[row, i + 2].Value = intensities[i];
                }
                row++;
            }

            return dataStartRow;
        }
    }
}
