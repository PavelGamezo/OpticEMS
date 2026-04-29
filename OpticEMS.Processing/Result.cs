namespace OpticEMS.Processing
{
    public class Result
    {
        /// <summary>
        /// Anomaly detection flag.
        /// </summary>
        public bool IsAnomaly { get; set; }

        /// <summary>
        /// Text description of the condition or error.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        public Result(bool isAnomaly, string message)
        {
            IsAnomaly = isAnomaly;
            Message = message;
        }
    }
}
