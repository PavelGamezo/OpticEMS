using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Common.Helpers;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace OpticEMS.MVVM.ViewModels.SettingsViewModels
{
    public partial class CalibrationSettingsChartViewModel : ObservableObject
    {
        [ObservableProperty]
        private ViewResolvingPlotModel _plotModel = SetUpModel();

        private static ViewResolvingPlotModel SetUpModel()
        {
            var model = new ViewResolvingPlotModel
            {
                PlotMargins = new OxyThickness(60, 40, 40, 60),
                Background = OxyColor.FromRgb(30, 29, 29),
                TextColor = OxyColors.White,
                DefaultFont = "Segoe UI",
                DefaultFontSize = 12,
                PlotAreaBorderThickness = new OxyThickness(1, 1, 0, 1),
                PlotAreaBorderColor = OxyColor.FromRgb(70, 70, 70),
                Padding = new OxyThickness(10),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Top,
                Title = "Pixel",
                TitleColor = OxyColors.White,
                TextColor = OxyColor.FromRgb(189, 195, 199),
                AxislineColor = OxyColor.FromRgb(70, 70, 70),
                TicklineColor = OxyColor.FromRgb(70, 70, 70),
                MajorGridlineColor = OxyColor.FromArgb(30, 236, 240, 241),
                MinorGridlineColor = OxyColor.FromArgb(15, 236, 240, 241),
                Minimum = 0,
                Maximum = 2047,
                Key = "pixelAxis"
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Wavelength",
                TitleColor = OxyColors.White,
                TextColor = OxyColor.FromRgb(189, 195, 199),
                AxislineColor = OxyColor.FromRgb(70, 70, 70),
                TicklineColor = OxyColor.FromRgb(70, 70, 70),
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromArgb(20, 255, 255, 255),
                MinorGridlineColor = OxyColor.FromArgb(15, 236, 240, 241),
                Minimum = 320,
                Maximum = 1080,
                Key = "wavelengthAxis"
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Intensity",
                TitleColor = OxyColors.White,
                TextColor = OxyColor.FromRgb(189, 195, 199),
                AxislineColor = OxyColor.FromRgb(70, 70, 70),
                TicklineColor = OxyColor.FromRgb(70, 70, 70),
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromArgb(20, 255, 255, 255),
                MinorGridlineColor = OxyColor.FromArgb(15, 236, 240, 241),
                Minimum = 0,
                Key = "intensityAxis"
            });

            model.Series.Add(new LineSeries
            {
                Title = "Spectrum",
                Color = OxyColor.FromRgb(52, 152, 219),
                StrokeThickness = 2,
                YAxisKey = "intensityAxis"
            });

            model.Series.Add(new LineSeries
            {
                Title = "Calibration graph",
                Color = OxyColor.FromRgb(46, 204, 113),
                StrokeThickness = 2,
                LineStyle = LineStyle.Solid,
                IsVisible = false,
                YAxisKey = "intensityAxis",
                XAxisKey = "wavelengthAxis"
            });

            return model;
        }

        public void ResetPlotSeries() 
        {
            PlotModel.Series.Clear();

            PlotModel.Series.Add(new LineSeries
            {
                Title = "Spectrum",
                Color = OxyColor.FromRgb(52, 152, 219),
                StrokeThickness = 2,
                YAxisKey = "intensityAxis"
            });

            PlotModel.Series.Add(new LineSeries
            {
                Title = "Calibration graph",
                Color = OxyColor.FromRgb(46, 204, 113),
                StrokeThickness = 2,
                LineStyle = LineStyle.Solid,
                IsVisible = false,
                YAxisKey = "intensityAxis",
                XAxisKey = "wavelengthAxis"
            });

            PlotModel.InvalidatePlot(true);
        }

        public void ResetPlotAxes() => PlotModel.ResetAllAxes();

        public void UpdateCalibrationPlot(uint[] data)
        {
            if (data == null)
            {
                return;
            }

            var lineSeries = PlotModel.Series.FirstOrDefault() as LineSeries;

            if (lineSeries == null)
            {
                lineSeries = new LineSeries
                {
                    Title = "Spectrum",
                    Color = OxyColors.LightBlue
                };

                PlotModel.Series.Add(lineSeries);
            }

            lineSeries.Points.Clear();

            for (int i = 0; i < data.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint(i, data[i]));
            }

            PlotModel.InvalidatePlot(true);
        }

        public void UpdateInterpolationPlot(uint[] currentSpectrumData, double[] wavelengths)
        {
            var calibrationSeries = PlotModel.Series
                .OfType<LineSeries>()
                .FirstOrDefault(s => s.Title == "Calibration graph");

            if (calibrationSeries == null)
            {
                return;
            }

            calibrationSeries.Points.Clear();

            for (uint i = 0; i < currentSpectrumData.Length; i++)
            {
                calibrationSeries.Points.Add(new DataPoint(wavelengths[i], currentSpectrumData[i]));
            }

            calibrationSeries.IsVisible = true;
            PlotModel.InvalidatePlot(true);
        }
    }
}
