using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using System.IO;
using System.Text;

namespace OpticEMS.Services.Export
{
    public class ExportManager : IExportManager
    {
        public void ExportAsTextFormat(string path, string recipeName, string channelName, List<double> wavelengths, List<TimePoint> points)
        {
            using var writer = new StreamWriter(path, false, Encoding.UTF8);
            var culture = System.Globalization.CultureInfo.InvariantCulture;

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

        public void ExportAsXLS(string path, string recipeName, string channelName, List<double> wavelengths, List<TimePoint> points)
        {
            var excelPackage = GenerateExcelPackage(recipeName, wavelengths, points);

            if (excelPackage != null)
            {
                File.WriteAllBytes(path, excelPackage);
            }
        }

        private byte[]? GenerateExcelPackage(string recipeName, List<double> wavelengths, List<TimePoint> points)
        {
            byte[] result;

            using (var package = new ExcelPackage())
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                ExcelWorksheet worksheets = package.Workbook.Worksheets.Add($"Data");

                worksheets.Cells[1, 1].Value = "Time (s)";
                worksheets.Cells[1, 1].Style.Font.Bold = true;

                for (int i = 0; i < wavelengths.Count; i++)
                {
                    var cell = worksheets.Cells[1, i + 2];
                    cell.Value = $"Intensity {wavelengths[i]:F2} nm";
                    cell.Style.Font.Bold = true;
                }

                var currentRow = 2;
                foreach (var point in points)
                {
                    worksheets.Cells[currentRow, 1].Value = point.TimeSeconds.ToString("F2", culture);

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
                        var x = worksheets.Cells[2, 1, currentRow - 1, 1];
                        var y = worksheets.Cells[2, i + 2, currentRow - 1, i + 2];

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
