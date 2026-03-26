using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Common.Helpers;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Windows.Media;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class SpectrumChartViewModel : ObservableObject
    {
        [ObservableProperty]
        private ViewResolvingPlotModel _plotModel = SetUpModel();
        
        public static ViewResolvingPlotModel SetUpModel()
        {
            var plotModel = new ViewResolvingPlotModel()
            {
                Background = OxyColor.FromRgb(31, 31, 31),
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColor.FromRgb(31, 31, 31),
                PlotAreaBorderThickness = new OxyThickness(4),
                PlotAreaBackground = OxyColor.FromRgb(31, 31, 31)
            };

            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Wavelength",
                TitleColor = OxyColors.White,
                TextColor = OxyColors.White,
                AxislineColor = OxyColor.FromRgb(70, 70, 70),
                TickStyle = TickStyle.None,
                MajorGridlineColor = OxyColor.FromRgb(50, 51, 56),
                MajorGridlineStyle = LineStyle.Solid
            });

            plotModel.Axes.Add(new LinearAxis
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

            var line = new LineSeries
            {
                Color = OxyColor.FromRgb(79, 195, 247),
                StrokeThickness = 2,
                LineStyle = LineStyle.Solid,
                MarkerType = MarkerType.None,
                CanTrackerInterpolatePoints = false,
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed,
                BrokenLineColor = OxyColors.Transparent
            };

            plotModel.Series.Add(line);

            return plotModel;
        }

        public void UpdateChart(IReadOnlyList<double> x, IReadOnlyList<uint> y)
        {
            if (PlotModel.Series[0] is not LineSeries line)
            {
                return;
            }

            line.Points.Clear();

            for (var i = 0; i < x.Count; i++)
            {
                line.Points.Add(new DataPoint(x[i], y[i]));
            }

            PlotModel.InvalidatePlot(true);
        }

        public void UpdateAnnotations(
            IReadOnlyList<double> targetWavelengths,
            IReadOnlyList<Color> wavelengthColors)
        {
            var oldAnnotations = PlotModel.Annotations
                .Where(annotations => annotations.Tag?.ToString() == "WavelengthMarker")
                .ToList();

            foreach (var old in oldAnnotations)
            {
                PlotModel.Annotations.Remove(old);
            }

            for (int i = 0; i < targetWavelengths.Count; i++)
            {
                var oxyColor = OxyColor.FromArgb(
                    wavelengthColors[i].A,
                    wavelengthColors[i].R,
                    wavelengthColors[i].G,
                    wavelengthColors[i].B);

                PlotModel.Annotations.Add(new LineAnnotation
                {
                    Type = LineAnnotationType.Vertical,
                    X = targetWavelengths[i],
                    Color = oxyColor,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 2,
                    Tag = "WavelengthMarker",
                    Text = $"Wavelength: {targetWavelengths[i]}nm"
                });
            }

            PlotModel.InvalidatePlot(false);
        }
        public void Dispose()
        {
            if (PlotModel != null)
            {
                PlotModel.Series.Clear();
                PlotModel.Annotations.Clear();
                PlotModel = null;
            }
        }
    }
}
