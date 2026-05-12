using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Common.Helpers;
using OpticEMS.Services.Etching;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Windows.Media;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class ProcessChartViewModel : ObservableObject
    {
        private readonly HashSet<string> _drawnConfirmedKeys = new();

        private readonly DateTime _epoch = new DateTime(2000, 1, 1);
        private bool _isMonitoringAreaActive;
        private bool _isOverEtchAreaActive;
        private bool _isDelayAreaActive;

        private RectangleAnnotation? _activeOverEtchArea;
        private RectangleAnnotation? _activeMonitoringArea;
        private RectangleAnnotation? _activeDelayArea;

        [ObservableProperty]
        private ViewResolvingPlotModel _plotModel;

        public void SetUpModel(List<double> targetWavelengths, List<Color> wavelengthColors)
        {
            _isMonitoringAreaActive = false;
            _isOverEtchAreaActive = false;
            _isDelayAreaActive = false;

            var model = new ViewResolvingPlotModel
            {
                PlotMargins = new OxyThickness(60, 40, 40, 40),
                Background = OxyColor.FromRgb(30, 29, 29),
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColor.FromRgb(30, 29, 29),
                PlotAreaBorderThickness = new OxyThickness(4),
                PlotAreaBackground = OxyColor.FromRgb(30, 29, 29)
            };

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "mm:ss",
                Title = "Time",
                IntervalType = DateTimeIntervalType.Seconds,
                TitleColor = OxyColors.White,
                TextColor = OxyColors.White,
                AxislineColor = OxyColor.FromRgb(50, 51, 56),
                MajorGridlineColor = OxyColor.FromRgb(50, 51, 56),
                MajorGridlineStyle = LineStyle.Solid,
                Minimum = DateTimeAxis.ToDouble(_epoch)
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Intensity",
                TitleColor = OxyColors.White,
                TextColor = OxyColors.White,
                AxislineColor = OxyColor.FromRgb(50, 51, 56),
                MajorGridlineColor = OxyColor.FromRgb(50, 51, 56),
                MajorGridlineStyle = LineStyle.Solid,
                MaximumPadding = 0.6,
                Minimum = 0
            });

            for (int i = 0; i < targetWavelengths.Count; i++)
            {
                var oxyColor = OxyColor.FromArgb(
                    wavelengthColors[i].A,
                    wavelengthColors[i].R,
                    wavelengthColors[i].G,
                    wavelengthColors[i].B);

                model.Series.Add(new LineSeries
                {
                    Title = $"Wavelength {i + 1}",
                    Color = oxyColor,
                    StrokeThickness = 2
                });
            }

            PlotModel = model;
        }

        public void UpdateTopPlot(TimeSpan elapsedTime, double[] intensities)
        {
            if (PlotModel == null)
            {
                return;
            }

            double xValue = DateTimeAxis.ToDouble(_epoch.Add(elapsedTime));

            lock (PlotModel.SyncRoot)
            {
                for (int i = 0; i < intensities.Length; i++)
                {
                    if (i < PlotModel.Series.Count && PlotModel.Series[i] is LineSeries line)
                    {
                        line.Points.Add(new DataPoint(xValue, intensities[i]));
                    }
                }
            }

            PlotModel.InvalidatePlot(true);
        }

        public void StartAnnotationArea(string status, TimeSpan elapsed)
        {
            if (status.Contains("Delay"))
            {
                if (!_isDelayAreaActive)
                {
                    MarkInitialDelay(elapsed);
                    StartInitialDelayArea(elapsed);
                    _isDelayAreaActive = true;
                }
                else
                {
                    UpdateInitialDelayArea(elapsed);
                }
            }
            else if (status.Contains("Monitoring"))
            {
                if (!_isMonitoringAreaActive)
                {
                    MarkEndpointMonitoring(elapsed);
                    StartEndpointMonitoringArea(elapsed);
                    _isMonitoringAreaActive = true;
                }
                else
                {
                    UpdateMonitoringArea(elapsed);
                }
            }
            else if (status.Contains("Over") || status.Contains("Endpoint Detected"))
            {
                if (!_isOverEtchAreaActive)
                {
                    MarkEndpoint(elapsed);
                    StartOverEtchArea(elapsed);
                    _isOverEtchAreaActive = true;
                }
                else
                {
                    UpdateOverEtchArea(elapsed);
                }
            }
        }

        #region Helpers for Annotations

        private void StartOverEtchArea(TimeSpan startTime)
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(startTime));

            var area = new RectangleAnnotation
            {
                MinimumX = xValue,
                MaximumX = xValue,
                Fill = OxyColor.FromAColor(30, OxyColors.OrangeRed),
                Stroke = OxyColors.OrangeRed,
                StrokeThickness = 1,
                Layer = AnnotationLayer.BelowSeries,
                Text = "OVER-ETCHING",
                FontWeight = FontWeights.Bold
            };

            AddAnnotationSafe(area);

            _activeOverEtchArea = area;
        }

        private void UpdateOverEtchArea(TimeSpan currentTime) =>
            UpdateRectMaximumX(ref _activeOverEtchArea, currentTime);

        private void StartEndpointMonitoringArea(TimeSpan startTime)
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(startTime));

            var area = new RectangleAnnotation
            {
                MinimumX = xValue,
                MaximumX = xValue,
                Fill = OxyColor.FromAColor(30, OxyColors.RoyalBlue),
                Stroke = OxyColors.RoyalBlue,
                StrokeThickness = 1,
                Layer = AnnotationLayer.BelowSeries,
                Text = "MONITORING",
                FontWeight = FontWeights.Bold
            };

            AddAnnotationSafe(area);

            _activeMonitoringArea = area;
        }

        private void UpdateMonitoringArea(TimeSpan currentTime) =>
            UpdateRectMaximumX(ref _activeMonitoringArea, currentTime);

        private void StartInitialDelayArea(TimeSpan startTime)
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(startTime));
            var area = new RectangleAnnotation
            {
                MinimumX = xValue,
                MaximumX = xValue,
                Fill = OxyColor.FromAColor(30, OxyColors.DarkSeaGreen),
                Stroke = OxyColors.DarkSeaGreen,
                StrokeThickness = 1,
                Layer = AnnotationLayer.BelowSeries,
                Text = "INITIAL DELAY",
                FontWeight = FontWeights.Bold
            };

            AddAnnotationSafe(area);
            _activeDelayArea = area;
        }

        private void UpdateInitialDelayArea(TimeSpan currentTime) =>
            UpdateRectMaximumX(ref _activeDelayArea, currentTime);

        private void MarkEndpoint(TimeSpan elapsedTime) =>
            AddVerticalLine(elapsedTime, OxyColors.Red, "Endpoint");

        private void MarkEndpointMonitoring(TimeSpan elapsedTime) =>
            AddVerticalLine(elapsedTime, OxyColors.RoyalBlue, "Monitoring");

        private void MarkInitialDelay(TimeSpan elapsedTime) =>
            AddVerticalLine(elapsedTime, OxyColors.DarkSeaGreen, "Start");

        #endregion

        #region Thread-Safe Core Methods

        private void AddAnnotationSafe(Annotation annotation)
        {
            if (PlotModel == null)
            {
                return;
            }

            lock (PlotModel.SyncRoot)
            {
                PlotModel.Annotations.Add(annotation);
            }

            PlotModel.InvalidatePlot(false);
        }

        public void DrawWindowBounds(
            List<WindowBounds> currentWindows,
            List<WindowBounds> confirmedInWindows,
            List<WindowBounds> confirmedOutWindows)
        {
            if (PlotModel == null)
            {
                return;
            }

            lock (PlotModel.SyncRoot)
            {
                var oldDynamic = PlotModel.Annotations
                    .Where(a => a.Tag?.ToString() == "DynamicWindow")
                    .ToList();

                foreach (var old in oldDynamic)
                {
                    PlotModel.Annotations.Remove(old);
                }

                foreach (var b in currentWindows)
                {
                    double startX = DateTimeAxis.ToDouble(_epoch.AddSeconds(b.StartTime));
                    double endX = DateTimeAxis.ToDouble(_epoch.AddSeconds(b.EndTime));

                    var rect = new RectangleAnnotation
                    {
                        Tag = "DynamicWindow",
                        MinimumX = startX,
                        MaximumX = endX,
                        MinimumY = b.Bottom,
                        MaximumY = b.Top,
                        Fill = OxyColor.FromAColor(35, OxyColors.White),
                        Stroke = OxyColor.FromAColor(100, OxyColors.LightGray),
                        StrokeThickness = 1,
                        Layer = AnnotationLayer.AboveSeries
                    };
                    PlotModel.Annotations.Add(rect);
                }

                AddNewConfirmedWindows(confirmedInWindows, "WindowIn", OxyColors.Yellow);
                AddNewConfirmedWindows(confirmedOutWindows, "WindowOut", OxyColors.LimeGreen);
            }

            PlotModel.InvalidatePlot(false);
        }

        private void AddNewConfirmedWindows(List<WindowBounds> confirmedList, string tagPrefix, OxyColor color)
        {
            foreach (var b in confirmedList)
            {
                string key = $"{tagPrefix}_{b.WavelengthIndex}_{b.EndTime:F3}";

                if (_drawnConfirmedKeys.Contains(key))
                {
                    continue;
                }

                double startX = DateTimeAxis.ToDouble(_epoch.AddSeconds(b.StartTime));
                double endX = DateTimeAxis.ToDouble(_epoch.AddSeconds(b.EndTime));

                var rect = new RectangleAnnotation
                {
                    Tag = tagPrefix,
                    MinimumX = startX,
                    MaximumX = endX,
                    MinimumY = b.Bottom,
                    MaximumY = b.Top,
                    Fill = OxyColor.FromAColor(45, color),
                    Stroke = color,
                    StrokeThickness = 1.5,
                    Layer = AnnotationLayer.AboveSeries,
                    Text = $"{tagPrefix} {b.WavelengthIndex + 1}",
                    TextColor = color,
                    FontSize = 9
                };

                PlotModel.Annotations.Add(rect);
                _drawnConfirmedKeys.Add(key);
            }
        }

        /*
        public void DrawWindowBounds(List<WindowBounds> windowBounds)
        {
            if (PlotModel == null)
            {
                return;
            }

            lock (PlotModel.SyncRoot)
            {
                var oldWindows = PlotModel.Annotations
                    .Where(a => a.Tag?.ToString() == "DynamicWindow")
                    .ToList();

                foreach (var old in oldWindows)
                {
                    PlotModel.Annotations.Remove(old);
                }

                foreach (var b in windowBounds)
                {
                    double startX = DateTimeAxis.ToDouble(_epoch.AddSeconds(b.StartTime));
                    double endX = DateTimeAxis.ToDouble(_epoch.AddSeconds(b.EndTime));

                    var rect = new RectangleAnnotation
                    {
                        Tag = "DynamicWindow",
                        MinimumX = startX,
                        MaximumX = endX,
                        MinimumY = b.Bottom,
                        MaximumY = b.Top,
                        Fill = OxyColor.FromAColor(40, OxyColors.White),
                        Stroke = OxyColor.FromAColor(120, OxyColors.Gray),
                        StrokeThickness = 1,
                        Layer = AnnotationLayer.AboveSeries
                    };
                    PlotModel.Annotations.Add(rect);
                }
            }

            PlotModel.InvalidatePlot(false);
        }*/

        private void UpdateRectMaximumX(ref RectangleAnnotation? area, TimeSpan currentTime)
        {
            if (area == null || PlotModel == null)
            {
                return;
            }

            double xValue = DateTimeAxis.ToDouble(_epoch.Add(currentTime));

            lock (PlotModel.SyncRoot)
            {
                area.MaximumX = xValue;
            }

            PlotModel.InvalidatePlot(false);
        }

        private void AddVerticalLine(TimeSpan time, OxyColor color, string text)
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(time));

            var line = new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                X = xValue,
                Color = color,
                LineStyle = LineStyle.Dash,
                StrokeThickness = 2,
                Text = text,
                TextColor = OxyColors.White,
                FontWeight = FontWeights.Bold
            };

            AddAnnotationSafe(line);
        }

        #endregion
    }
}
