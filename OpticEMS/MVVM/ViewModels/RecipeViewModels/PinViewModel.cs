using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace OpticEMS.MVVM.ViewModels.RecipeViewModels
{
    public class PinViewModel : ObservableObject
    {
        public string Name { get; }

        private Point _bindingPoint;
        public Point BindingPoint
        {
            get => _bindingPoint;
            set => SetProperty(ref _bindingPoint, value);
        }

        public NodeViewModel ParentNode { get; }

        public PinViewModel(NodeViewModel parent, string name)
        {
            ParentNode = parent;
            Name = name;
        }
    }
}
