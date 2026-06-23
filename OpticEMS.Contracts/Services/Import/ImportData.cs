namespace OpticEMS.Contracts.Services.Import
{
    public record ImportData
    {
        public DateTime StartTime { get; init; }
        public DateTime EndTime { get; init; }
        public DateTime OverEtchStartTime { get; init; }
        public DateTime OverEtchEndTime { get; init; }
        public string RecipeName { get; init; } = string.Empty;
        public string ChannelName { get; init; } = string.Empty;

        public IReadOnlyList<TraceSeries> Series { get; init; }
            = Array.Empty<TraceSeries>();

        public double DurationSeconds =>
            Series.SelectMany(s => s.Points).Select(p => p.TimeSeconds)
                  .DefaultIfEmpty(0).Max();
    }
}
