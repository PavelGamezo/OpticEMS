using CommunityToolkit.Mvvm.ComponentModel;
using OpticEMS.Common.Helpers;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Diagnostics;
using System.Windows.Media;

namespace OpticEMS.MVVM.ViewModels.ProcessViewModels
{
    public partial class ProcessChartViewModel : ObservableObject
    {
        private readonly DateTime _epoch = new DateTime(2000, 1, 1);
        private bool _isMonitoringAreaActive;
        private bool _isOverEtchAreaActive;
        private RectangleAnnotation? _activeOverEtchArea;
        private RectangleAnnotation? _activeMonitoringArea;
        private RectangleAnnotation? _activeDelayArea;

        [ObservableProperty]
        private ViewResolvingPlotModel _plotModel;

        public void SetUpModel(List<double> targetWavelengths, List<Color> wavelengthColors)
        {
            PlotModel = new ViewResolvingPlotModel
            {
                PlotMargins = new OxyThickness(60, 40, 40, 40),
                Background = OxyColor.FromRgb(30, 29, 29),
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColor.FromRgb(30, 29, 29),
                PlotAreaBorderThickness = new OxyThickness(4),
                PlotAreaBackground = OxyColor.FromRgb(30, 29, 29)
            };

            PlotModel.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "mm:ss",
                Title = "Time",
                IntervalType = DateTimeIntervalType.Seconds,
                TitleColor = OxyColors.White,
                TextColor = OxyColors.White,
                AxislineColor = OxyColor.FromRgb(50, 51, 56),
                TickStyle = TickStyle.None,
                MajorGridlineColor = OxyColor.FromRgb(50, 51, 56),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineColor = OxyColor.FromArgb(15, 236, 240, 241),
                Minimum = DateTimeAxis.ToDouble(_epoch)
            });

            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Intensity",
                TitleColor = OxyColors.White,
                TextColor = OxyColors.White,
                AxislineColor = OxyColor.FromRgb(50, 51, 56),
                TickStyle = TickStyle.None,
                MajorGridlineColor = OxyColor.FromRgb(50, 51, 56),
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineColor = OxyColor.FromArgb(15, 236, 240, 241),
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

                var target = new LineSeries
                {
                    Title = $"Wavelength {i + 1}",
                    Color = oxyColor,
                    StrokeThickness = 2,
                    MarkerSize = 3,
                    MarkerFill = OxyColors.Blue
                };

                PlotModel.Series.Add(target);
            }
        }

        public void UpdateTopPlot(TimeSpan elapsedTime, uint[] intensities)
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(elapsedTime));

            for (int i = 0; i < intensities.Length; i++)
            {
                if (PlotModel.Series[i] is LineSeries firstLine)
                {
                    firstLine.Points.Add(new DataPoint(xValue, intensities[i]));
                }
            }

            PlotModel.InvalidatePlot(true);
        }

        public void StartAnnotationArea(string status, Stopwatch stopwatch)
        {
            if (status.Contains("Monitoring"))
            {
                if (!_isMonitoringAreaActive)
                {
                    MarkEndpointMonitoring(stopwatch.Elapsed);
                    StartEndpointMonitoringArea(stopwatch.Elapsed);
                    _isMonitoringAreaActive = true;
                }
                else
                {
                    UpdateMonitoringArea(stopwatch.Elapsed);
                }
            }
            else if (status.Contains("Over") || status.Contains("Endpoint Detected"))
            {
                if (!_isOverEtchAreaActive)
                {
                    MarkEndpoint(stopwatch.Elapsed);
                    StartOverEtchArea(stopwatch.Elapsed);
                    _isOverEtchAreaActive = true;
                }
                else
                {
                    UpdateOverEtchArea(stopwatch.Elapsed);
                }
            }
        }

        private void StartOverEtchArea(TimeSpan startTime)
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(startTime));

            _activeOverEtchArea = new RectangleAnnotation
            {
                MinimumX = xValue,
                MaximumX = xValue,
                Fill = OxyColor.FromAColor(30, OxyColors.OrangeRed),
                Stroke = OxyColors.OrangeRed,
                StrokeThickness = 1,
                Layer = AnnotationLayer.BelowSeries,
                Text = "OVER-ETCHING",
                FontWeight = FontWeights.Bold,
                Font = "Segoe UI Semibold",
            };

            PlotModel.Annotations.Add(_activeOverEtchArea);
            PlotModel.InvalidatePlot(false);
        }

        private void UpdateOverEtchArea(TimeSpan currentTime)
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(currentTime));

            if (_activeOverEtchArea != null)
            {
                _activeOverEtchArea.MaximumX = xValue;
                PlotModel.InvalidatePlot(false);
            }
        }

        private void MarkEndpoint(TimeSpan elapsedTime, string label = "Endpoint")
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(elapsedTime));

            var marker = new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                X = xValue,
                Color = OxyColors.Red,
                LineStyle = LineStyle.Dash,
                StrokeThickness = 2,
                Text = label,
                FontWeight = FontWeights.Bold,
                Font = "Segoe UI Semibold",
                TextColor = OxyColors.White
            };

            PlotModel.Annotations.Add(marker);
            PlotModel.InvalidatePlot(false);
        }

        private void MarkEndpointMonitoring(TimeSpan elapsedTime, string label = "Monitoring")
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(elapsedTime));

            var marker = new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                X = xValue,
                Color = OxyColors.RoyalBlue,
                LineStyle = LineStyle.Dash,
                StrokeThickness = 2,
                Text = label,
                FontWeight = FontWeights.Bold,
                Font = "Segoe UI Semibold",
                TextColor = OxyColors.White
            };

            PlotModel.Annotations.Add(marker);
            PlotModel.InvalidatePlot(false);
        }

        private void StartEndpointMonitoringArea(TimeSpan startTime)
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(startTime));

            _activeMonitoringArea = new RectangleAnnotation
            {
                MinimumX = xValue,
                MaximumX = xValue,
                Fill = OxyColor.FromAColor(30, OxyColors.RoyalBlue),
                Stroke = OxyColors.RoyalBlue,
                StrokeThickness = 1,
                Layer = AnnotationLayer.BelowSeries,
                Text = "MONITORING",
                FontWeight = FontWeights.Bold,
                Font = "Segoe UI Semibold",
            };

            PlotModel.Annotations.Add(_activeMonitoringArea);
            PlotModel.InvalidatePlot(false);
        }

        private void UpdateMonitoringArea(TimeSpan currentTime)
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(currentTime));

            if (_activeMonitoringArea != null)
            {
                _activeMonitoringArea.MaximumX = xValue;
                PlotModel.InvalidatePlot(false);
            }
        }

        private void MarkInitialDelay(TimeSpan elapsedTime, string label = "InitialDelay")
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(elapsedTime));

            var marker = new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                X = xValue,
                Color = OxyColors.DarkSeaGreen,
                LineStyle = LineStyle.Dash,
                StrokeThickness = 2,
                Text = label,
                FontWeight = FontWeights.Bold,
                Font = "Segoe UI Semibold",
                TextColor = OxyColors.White
            };

            PlotModel.Annotations.Add(marker);
            PlotModel.InvalidatePlot(false);
        }

        private void StartInitialDelayArea(TimeSpan startTime)
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(startTime));

            _activeDelayArea = new RectangleAnnotation
            {
                MinimumX = xValue,
                MaximumX = xValue,
                Fill = OxyColor.FromAColor(30, OxyColors.DarkSeaGreen),
                Stroke = OxyColors.DarkSeaGreen,
                StrokeThickness = 1,
                Layer = AnnotationLayer.BelowSeries,
                Text = "INITIAL DELAY",
                FontWeight = FontWeights.Bold,
                Font = "Segoe UI Semibold",
            };

            PlotModel.Annotations.Add(_activeDelayArea);
            PlotModel.InvalidatePlot(false);
        }

        private void UpdateInitialDelayArea(TimeSpan currentTime)
        {
            double xValue = DateTimeAxis.ToDouble(_epoch.Add(currentTime));

            if (_activeDelayArea != null)
            {
                _activeDelayArea.MaximumX = xValue;
                PlotModel.InvalidatePlot(false);
            }
        }
    }
}
