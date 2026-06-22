using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Common.Helpers;
using OpticEMS.Processing.SpectrumScanner;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class SpectrumScanChartViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private ViewResolvingPlotModel _plotModel;

        private readonly LineSeries _peakSeries;
        private readonly LineAnnotation _thresholdLine;

        public SpectrumScanChartViewModel(int trimLeft, int trimRight)
        {
            PlotModel = BuildModel(out _peakSeries, out _thresholdLine, trimLeft, trimRight);
        }

        private static ViewResolvingPlotModel BuildModel(
            out LineSeries peakSeries,
            out LineAnnotation thresholdLine,
            int trimLeft, int trimRight)
        {
            var model = new ViewResolvingPlotModel
            {
                Background = OxyColor.FromRgb(31, 31, 31),
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColor.FromRgb(31, 31, 31),
                PlotAreaBorderThickness = new OxyThickness(4),
                PlotAreaBackground = OxyColor.FromRgb(31, 31, 31)
            };

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Wavelength",
                TitleColor = OxyColors.White,
                TextColor = OxyColors.White,
                Minimum = trimLeft,
                Maximum = trimRight,
                AxislineColor = OxyColor.FromRgb(70, 70, 70),
                TickStyle = TickStyle.None,
                MajorGridlineColor = OxyColor.FromRgb(50, 51, 56),
                MajorGridlineStyle = LineStyle.Solid,
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Intensity",
                TitleColor = OxyColors.White,
                TextColor = OxyColor.FromRgb(189, 195, 199),
                AxislineColor = OxyColor.FromRgb(52, 73, 94),
                TickStyle = TickStyle.None,
                MajorGridlineColor = OxyColor.FromRgb(50, 51, 56),
                MajorGridlineStyle = LineStyle.Solid,
                MaximumPadding = 0.3,
                Minimum = 0
            });

            peakSeries = new LineSeries
            {
                Title = "Peaks",
                Color = OxyColor.FromRgb(79, 195, 247),
                StrokeThickness = 2,
                LineStyle = LineStyle.Solid,
                MarkerType = MarkerType.None,
                CanTrackerInterpolatePoints = false,
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed,
                BrokenLineColor = OxyColors.Transparent
            };

            model.Series.Add(peakSeries);

            thresholdLine = new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Y = 0,
                Color = OxyColor.FromRgb(241, 196, 15),
                LineStyle = LineStyle.Dash,
                StrokeThickness = 1.5,
                Text = "Threshold",
                TextColor = OxyColor.FromRgb(241, 196, 15),
                Tag = "Threshold"
            };

            model.Annotations.Add(thresholdLine);

            return model;
        }

        public void Update(SpectrumScannerResult result)
        {
            if (PlotModel is null)
            {
                return;
            }

            _peakSeries.Points.Clear();

            int count = Math.Min(result.Wavelengths.Length, result.Intensities.Length);
            for (int i = 0; i < count; i++)
            {
                _peakSeries.Points.Add(new DataPoint(result.Wavelengths[i], result.Intensities[i]));
            }

            _thresholdLine.Y = result.Threshold;
            _thresholdLine.Text = $"Threshold ({result.Threshold:F0})";

            PlotModel.InvalidatePlot(true);
        }

        public void Dispose()
        {
            if (PlotModel is not null)
            {
                PlotModel.Series.Clear();
                PlotModel.Annotations.Clear();
                PlotModel = null;
            }
        }
    }
}