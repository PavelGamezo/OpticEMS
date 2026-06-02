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
        private ViewResolvingPlotModel _plotModel;

        private RectangleAnnotation _anomalyRegion;
        private IReadOnlyList<double> _lastX;

        public event Action<int, double> OnWavelengthMoved;

        public SpectrumChartViewModel(int trimLeft, int trimRight)
        {
            PlotModel = SetUpModel(trimLeft, trimRight);

            _anomalyRegion = PlotModel.Annotations
                .OfType<RectangleAnnotation>()
                .First(a => a.Tag?.ToString() == "AnomalyRegion");
        }

        public static ViewResolvingPlotModel SetUpModel(int trimLeft, int trimRight)
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
                MajorGridlineStyle = LineStyle.Solid,
                Minimum = trimLeft,
                Maximum = trimRight,
                AbsoluteMinimum = trimLeft,
                AbsoluteMaximum = trimRight
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

            var anomaly = new RectangleAnnotation
            {
                Tag = "AnomalyRegion",
                Fill = OxyColor.FromArgb(40, 255, 80, 80),
                Stroke = OxyColor.FromArgb(120, 255, 120, 120),
                StrokeThickness = 1.5,
                Layer = AnnotationLayer.AboveSeries,
                MinimumX = 0,
                MaximumX = 0,
                MinimumY = 0,
                MaximumY = 0
            };

            plotModel.Annotations.Add(anomaly);

            return plotModel;
        }

        public void UpdateChart(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            _lastX = x;

            if (PlotModel.Series[0] is not LineSeries line)
                return;

            line.Points.Clear();

            for (var i = 0; i < x.Count; i++)
                line.Points.Add(new DataPoint(x[i], y[i]));

            PlotModel.InvalidatePlot(true);
        }

        public void UpdateAnomaly(List<(int start, int end)> ranges)
        {
            lock (PlotModel.SyncRoot)
            {
                if (_lastX == null)
                {
                    return;
                }

                var old = PlotModel.Annotations
                    .Where(a => a.Tag?.ToString() == "AnomalyRegion")
                    .ToList();

                foreach (var annotation in old)
                {
                    PlotModel.Annotations.Remove(annotation);
                }

                if (ranges == null || ranges.Count == 0)
                {
                    PlotModel.InvalidatePlot(false);

                    return;
                }

                var yAxis = PlotModel.Axes[1];

                foreach (var (start, end) in ranges)
                {
                    if (start < 0 || end < 0 || start >= _lastX.Count || end >= _lastX.Count)
                    {
                        continue;
                    }

                    int s = Math.Max(0, start - 15);
                    int e = Math.Min(_lastX.Count - 1, end + 15);

                    double startX = _lastX[s];
                    double endX = _lastX[e];

                    var region = new RectangleAnnotation
                    {
                        Tag = "AnomalyRegion",
                        Fill = OxyColor.FromArgb(60, 255, 80, 80),
                        Stroke = OxyColor.FromArgb(160, 255, 120, 120),
                        StrokeThickness = 1.5,
                        Layer = AnnotationLayer.AboveSeries,

                        MinimumX = startX,
                        MaximumX = endX,

                        MinimumY = yAxis.ActualMinimum,
                        MaximumY = yAxis.ActualMaximum
                    };

                    PlotModel.Annotations.Add(region);
                }
            }

            PlotModel.InvalidatePlot(false);
        }

        public void UpdateAnnotations(
            IList<double> targetWavelengths,
            IReadOnlyList<Color> wavelengthColors,
            IList<bool> isRecipeFlags)
        {
            var toRemove = PlotModel.Annotations
                .Where(annotation =>
                    annotation.Tag?.ToString() == "CatalogWavelength" ||
                    annotation.Tag?.ToString() == "RecipeWavelength")
                .ToList();

            foreach (var old in toRemove)
            {
                PlotModel.Annotations.Remove(old);
            }

            for (int i = 0; i < targetWavelengths.Count; i++)
            {
                int index = i;

                var oxyColor = OxyColor.FromArgb(
                    wavelengthColors[i].A,
                    wavelengthColors[i].R,
                    wavelengthColors[i].G,
                    wavelengthColors[i].B);

                bool isRecipe = isRecipeFlags[i];

                var annotation = new LineAnnotation
                {
                    Type = LineAnnotationType.Vertical,
                    X = targetWavelengths[i],
                    Color = oxyColor,
                    LineStyle = isRecipe ? LineStyle.Dash : LineStyle.Dot,
                    StrokeThickness = isRecipe ? 2 : 1,
                    Tag = isRecipe ? "RecipeWavelength" : "CatalogWavelength",
                    Text = $"{targetWavelengths[i]}nm",
                    ClipByYAxis = true,
                };

                if (isRecipe)
                {
                    annotation.MouseDown += (s, e) =>
                    {
                        if (e.ChangedButton == OxyMouseButton.Left)
                        {
                            annotation.StrokeThickness = 4;
                            PlotModel.InvalidatePlot(false);
                            e.Handled = true;
                        }
                    };

                    annotation.MouseMove += (s, e) =>
                    {
                        double newX = annotation.InverseTransform(e.Position).X;
                        newX = Math.Round(newX, 2);

                        annotation.X = newX;
                        annotation.Text = $"{newX:F2}nm";

                        if (index < targetWavelengths.Count)
                            targetWavelengths[index] = newX;

                        PlotModel.InvalidatePlot(false);
                        e.Handled = true;
                    };

                    annotation.MouseUp += (s, e) =>
                    {
                        annotation.StrokeThickness = 2;
                        OnWavelengthMoved?.Invoke(index, annotation.X);
                        PlotModel.InvalidatePlot(false);
                        e.Handled = true;
                    };
                }

                PlotModel.Annotations.Add(annotation);
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
