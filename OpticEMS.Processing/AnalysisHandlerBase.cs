namespace OpticEMS.Processing
{
    public abstract class AnalysisHandlerBase<T> where T : AnalysisSpectrumBase
    {
        protected readonly T Analyzer;

        public string Status { get; protected set; } = "None";

        protected AnalysisHandlerBase(T analyzer)
        {
            Analyzer = analyzer;
        }

        /// <summary>
        /// Orchestrates the full analysis cycle for the current spectrum: evaluates model readiness, 
        /// performs automated training using provided history if necessary, and calculates statistical deviations.
        /// </summary>
        /// <param name="currentSpectrum">The latest intensity array received from the detector.</param>
        /// <param name="history">A collection of previous spectral snapshots used for statistical accumulation or model training.</param>
        /// <returns>
        /// A <see cref="Result"/> object containing the analysis verdict (normal/anomaly) 
        /// and a detailed message regarding the current process state.
        /// </returns>
        public abstract Result Process(double[] currentSpectrum);

        public abstract Task<Result> ProcessAsync(double[] currentSpectrum);
    }
}
