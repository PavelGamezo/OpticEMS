using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;

namespace OpticEMS.MVVM.ViewModels.RecipeViewModels
{
    public enum NodeType
    {
        SpectralLine,
        Filter,
        Derivative,
        Addition,
        Subtraction,
        Division,
        Multiplication,
        Sink
    }

    public partial class NodeViewModel : ObservableObject
    {

        public string Id { get; set; } = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private Point _location;

        [ObservableProperty]
        private bool _isSelected;

        public NodeType Type { get; set; }

        public ObservableCollection<PinViewModel> InputPins { get; } = new();
        public ObservableCollection<PinViewModel> OutputPins { get; } = new();

        [ObservableProperty]
        private double filterPeriod;

        [ObservableProperty]
        private int periodToAverage;

        [ObservableProperty]
        private int derivativeTime = 5;

        public Dictionary<string, object> GetPropertiesAsDictionary()
        {
            var dict = new Dictionary<string, object>
            {
                ["x"] = Location.X,
                ["y"] = Location.Y
            };

            switch (Type)
            {
                case NodeType.Filter:
                    dict["magneticFieldPeriodMs"] = FilterPeriod;
                    dict["periodsToAverage"] = PeriodToAverage;
                    break;

                case NodeType.Derivative:
                    dict["derivationTime"] = DerivativeTime;
                    break;

                case NodeType.SpectralLine:
                case NodeType.Sink:
                    dict["title"] = Title;
                    break;
            }

            return dict;
        }
    }
}
