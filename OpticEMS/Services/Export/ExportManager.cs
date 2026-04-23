using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using OpticEMS.Contracts.Services.Export;
using System.IO;
using System.Text;

namespace OpticEMS.Services.Export
{
    public class ExportManager : IExportManager
    {
        public void ExportAsTextFormat(string path, DateTime startTime, DateTime endTime, DateTime overEtchStartTime,
            DateTime overEtchEndTime, string recipeName, string channelName, List<double> wavelengths, List<TimePoint> points)
        {
            using var writer = new StreamWriter(path, false, Encoding.UTF8);
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            writer.WriteLine($"Start Process Time: {startTime}");
            writer.WriteLine($"End Process Time: {endTime}");
            writer.WriteLine($"Overetching Time: {overEtchStartTime} to {overEtchEndTime}");
            writer.WriteLine($"Export Date: {DateTime.Now}");
            writer.WriteLine($"Recipe: {recipeName ?? "N/A"}");
            writer.WriteLine($"Channel: {channelName}");

            writer.Write("Time (s)");

            foreach (var wavelength in wavelengths)
            {
                writer.Write($";Intensity {wavelength.ToString("F2", culture)} nm");
            }
            writer.WriteLine();

            foreach (var point in points)
            {
                writer.Write(point.TimeSeconds.ToString("F2", culture));

                foreach (var intensity in point.Intensities)
                {
                    writer.Write($";{intensity}");
                }
                writer.WriteLine();
            }
        }

        public void ExportAsXLS(string path, DateTime startTime, DateTime endTime, DateTime overEtchStartTime,
            DateTime overEtchEndTime, string recipeName, string channelName, List<double> wavelengths, List<TimePoint> points)
        {
            var excelPackage = GenerateExcelPackage(startTime, endTime, overEtchStartTime,
                overEtchEndTime, recipeName, channelName, wavelengths, points);

            if (excelPackage != null)
            {
                File.WriteAllBytes(path, excelPackage);
            }
        }

        private byte[]? GenerateExcelPackage(DateTime startTime, DateTime endTime, DateTime overEtchStartTime,
            DateTime overEtchEndTime, string recipeName, string channelName, List<double> wavelengths, List<TimePoint> points)
        {
            byte[] result;

            using (var package = new ExcelPackage())
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                ExcelWorksheet worksheets = package.Workbook.Worksheets.Add($"Data");

                worksheets.Cells[1, 1].Value = "Start Process Time: ";
                worksheets.Cells[1, 1].Style.Font.Bold = true;
                worksheets.Cells[1, 2].Value = startTime.ToString("dd.MM.yyyy HH:mm:ss");

                worksheets.Cells[2, 1].Value = "End Process Time: ";
                worksheets.Cells[2, 1].Style.Font.Bold = true;
                worksheets.Cells[2, 2].Value = endTime.ToString("dd.MM.yyyy HH:mm:ss");

                worksheets.Cells[3, 1].Value = "Endpoint Detection Time: ";
                worksheets.Cells[3, 1].Style.Font.Bold = true;
                worksheets.Cells[3, 2].Value = overEtchStartTime.ToString("dd.MM.yyyy HH:mm:ss");

                worksheets.Cells[4, 1].Value = "Overetching Time: ";
                worksheets.Cells[4, 1].Style.Font.Bold = true;
                worksheets.Cells[4, 2].Value = overEtchStartTime.ToString("dd.MM.yyyy HH:mm:ss");
                worksheets.Cells[4, 3].Value = "to";
                worksheets.Cells[4, 4].Value = overEtchEndTime.ToString("dd.MM.yyyy HH:mm:ss");

                worksheets.Cells[5, 1].Value = "Export Time:";
                worksheets.Cells[5, 1].Style.Font.Bold = true;
                worksheets.Cells[5, 2].Value = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");

                worksheets.Cells[6, 1].Value = "Recipe:";
                worksheets.Cells[6, 1].Style.Font.Bold = true;
                worksheets.Cells[6, 2].Value = recipeName;

                worksheets.Cells[7, 1].Value = "Channel:";
                worksheets.Cells[7, 1].Style.Font.Bold = true;
                worksheets.Cells[7, 2].Value = channelName;

                worksheets.Cells[8, 1].Value = "Time (s)";
                worksheets.Cells[8, 1].Style.Font.Bold = true;

                for (int i = 0; i < wavelengths.Count; i++)
                {
                    var cell = worksheets.Cells[8, i + 2];
                    cell.Value = $"Intensity {wavelengths[i]:F2} nm";
                    cell.Style.Font.Bold = true;
                }

                var currentRow = 9;
                foreach (var point in points)
                {
                    worksheets.Cells[currentRow, 1].Value = point.TimeSeconds;
                    worksheets.Cells[currentRow, 1].Style.Numberformat.Format = "0.00";

                    for (int i = 0; i < point.Intensities.Count; i++)
                    {
                        worksheets.Cells[currentRow, i + 2].Value = point.Intensities[i];
                    }

                    currentRow++;
                }

                if (points.Count > 1)
                {
                    ExcelChart chart = worksheets.Drawings.AddChart("SpectrumChart", eChartType.XYScatterLinesNoMarkers);

                    chart.Title.Text = $"Etching Process: {recipeName}";

                    for (int i = 0; i < wavelengths.Count; i++)
                    {
                        var x = worksheets.Cells[9, 1, currentRow - 1, 1];
                        var y = worksheets.Cells[9, i + 2, currentRow - 1, i + 2];

                        var series = chart.Series.Add(y, x);
                        series.Header = $"{wavelengths[i]:F3} nm";
                    }

                    chart.SetPosition(1, 0, wavelengths.Count + 3, 0);
                    chart.SetSize(800, 400);

                    chart.XAxis.Title.Text = "Time (seconds)";
                    chart.YAxis.Title.Text = "Intensity";
                }

                result = package.GetAsByteArray();
            }

            return result;
        }
    }
}
