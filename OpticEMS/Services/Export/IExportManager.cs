namespace OpticEMS.Services.Export
{
    public interface IExportManager
    {
        void ExportAsTextFormat(string path, string recipeName, string channelName, List<double> wavelengths, List<TimePoint> points);

        void ExportAsXLS(string path, string recipeName, string channelName, List<double> wavelengths, List<TimePoint> points);
    }
}
