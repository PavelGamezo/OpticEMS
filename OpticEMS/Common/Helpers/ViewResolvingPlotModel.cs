using OxyPlot;
using System.Reflection;

namespace OpticEMS.Common.Helpers
{
    public class ViewResolvingPlotModel : PlotModel, IPlotModel
    {
        private static readonly Type BaseType = typeof(ViewResolvingPlotModel).BaseType;

        private static readonly MethodInfo BaseAttachMethod = BaseType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .FirstOrDefault(methodInfo => methodInfo.IsFinal && methodInfo.IsPrivate
            && methodInfo.Name.EndsWith(nameof(IPlotModel.AttachPlotView)));

        void IPlotModel.AttachPlotView(IPlotView plotView)
        {
            if (plotView != null && PlotView != null && !Equals(plotView, PlotView))
            {
                BaseAttachMethod.Invoke(this, new object[] { null });
                BaseAttachMethod.Invoke(this, new object[] { plotView });
            }
            else
            {
                BaseAttachMethod.Invoke(this, new object[] { plotView });
            }
        }
    }
}
