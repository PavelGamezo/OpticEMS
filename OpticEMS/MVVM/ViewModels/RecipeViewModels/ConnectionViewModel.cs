using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Windows;

namespace OpticEMS.MVVM.ViewModels.RecipeViewModels
{
    public class ConnectionViewModel : ObservableObject
    {
        public string SourceNodeId => Source?.ParentNode?.Id ?? string.Empty;
        public string TargetNodeId => Target?.ParentNode?.Id ?? string.Empty;

        private PinViewModel _source;
        public PinViewModel Source
        {
            get => _source;
            set
            {
                if (_source != null) { _source.PropertyChanged -= OnPinChanged; if (_source.ParentNode != null) _source.ParentNode.PropertyChanged -= OnNodeChanged; }
                if (SetProperty(ref _source, value) && _source != null)
                {
                    _source.PropertyChanged += OnPinChanged;
                    if (_source.ParentNode != null) _source.ParentNode.PropertyChanged += OnNodeChanged;
                }
            }
        }

        private PinViewModel _target;
        public PinViewModel Target
        {
            get => _target;
            set
            {
                if (_target != null) { _target.PropertyChanged -= OnPinChanged; if (_target.ParentNode != null) _target.ParentNode.PropertyChanged -= OnNodeChanged; }
                if (SetProperty(ref _target, value) && _target != null)
                {
                    _target.PropertyChanged += OnPinChanged;
                    if (_target.ParentNode != null) _target.ParentNode.PropertyChanged += OnNodeChanged;
                }
            }
        }

        public Point SourcePoint
        {
            get
            {
                if (Source == null || Source.ParentNode == null)
                {
                    return new Point(0, 0);
                }

                double x = Source.BindingPoint.X;
                double y = Source.BindingPoint.Y;

                if (x == 0 && y == 0)
                {
                    x = 150;
                    y = 36;
                }

                return new Point(Source.ParentNode.Location.X + x, Source.ParentNode.Location.Y + y);
            }
        }

        public Point TargetPoint
        {
            get
            {
                if (Target == null || Target.ParentNode == null)
                {
                    return new Point(0, 0);
                }

                double x = Target.BindingPoint.X;
                double y = Target.BindingPoint.Y;

                if (x == 0 && y == 0)
                {
                    x = 10;

                    if (Target.Name == "B")
                    {
                        y = 56;
                    }
                    else
                    {
                        y = 36;
                    }
                }

                return new Point(Target.ParentNode.Location.X + x, Target.ParentNode.Location.Y + y);
            }
        }

        public ConnectionViewModel(PinViewModel source, PinViewModel target)
        {
            Source = source;
            Target = target;
        }

        private void OnPinChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PinViewModel.BindingPoint))
            {
                NotifyPoints();
            }
        }

        private void OnNodeChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NodeViewModel.Location))
            {
                NotifyPoints();
            }
        }

        private void NotifyPoints()
        {
            OnPropertyChanged(nameof(SourcePoint));
            OnPropertyChanged(nameof(TargetPoint));
        }
    }
}
