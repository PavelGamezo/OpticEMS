using OpticEMS.Services.Export;
using OxyPlot;

namespace OpticEMS.Common.Helpers.OxyPlot
{
    public static class OxyPlotHelper
    {
        public static Tuple<IExporter, string> GetExportInfo(
            OxyExportType type, DateTime startTime, DateTime endTime, 
            DateTime overEtchStartTime, DateTime overEtchEndTime,
            string recipeName, string channelName, string path)
        {
            switch (type)
            {
                case OxyExportType.Text:
                    return new Tuple<IExporter, string>(new OxyTextExporter(
                        startTime, endTime, overEtchStartTime,
                        overEtchEndTime, recipeName, channelName), 
                        path);

                case OxyExportType.CommaSeparatedValues:
                    return new Tuple<IExporter, string>(new OxyCsvExporter(
                        startTime, endTime, overEtchStartTime,
                        overEtchEndTime, recipeName, channelName),
                        path);

                case OxyExportType.Excel:
                    return new Tuple<IExporter, string>(new OxyExcelExporter(
                        startTime, endTime, overEtchStartTime,
                        overEtchEndTime, recipeName, channelName),
                        path);

                default:
                    return new Tuple<IExporter, string>(new OxyTextExporter(
                        startTime, endTime, overEtchStartTime,
                        overEtchEndTime, recipeName, channelName),
                        path);
            }
        }
    }
}
