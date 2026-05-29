namespace OpticEMS.Common.Helpers.OxyPlot
{
    public record ExportData(
        DateTime startTime, DateTime endTime, DateTime overEtchStartTime,
        DateTime overEtchEndTime, string recipeName, string channelName);
}
