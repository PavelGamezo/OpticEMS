namespace OpticEMS.Services.Export
{
    public interface IExportManager
    {
        void ExportAsTextFormat(string path, DateTime startTime, DateTime endTime, DateTime overEtchStartTime,
            DateTime overEtchEndTime, string recipeName, string channelName, List<double> wavelengths, List<TimePoint> points);

        void ExportAsXLS(string path, DateTime startTime, DateTime endTime, DateTime overEtchStartTime,
            DateTime overEtchEndTime,string recipeName, string channelName, List<double> wavelengths, List<TimePoint> points);
    }
}
