namespace OpticEMS.Contracts.Services.Export
{
    public record ExportData(
        DateTime StartTime,
        DateTime EndTime,
        DateTime OverEtchStartTime,
        DateTime OverEtchEndTime);
}
