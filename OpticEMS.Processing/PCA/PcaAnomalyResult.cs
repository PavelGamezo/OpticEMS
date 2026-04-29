namespace OpticEMS.Processing.PCA
{
    public class PcaAnomalyResult : Result
    {
        /// <summary>
        /// Hotelling statistics (T²).
        /// Shows how far the current spectrum has moved from the center of the "normal" state
        /// within the model. An increase in T² usually indicates a phase shift in the process 
        /// (e.g., the onset of etching).
        /// </summary>
        public double T2 { get; set; }

        /// <summary>
        /// Q statistic (reconstruction error).
        /// Shows the portion of the spectrum that the PCA model "didn't recognize."
        /// A sharp jump in Q indicates the appearance of new lines or anomalous noise.
        /// </summary>
        public double Q { get; set; }

        /// <summary>
        /// Threshold for the T² statistic calculated during training.
        /// </summary>
        public double T2Limit { get; set; }

        /// <summary>
        /// Threshold for the Q statistic calculated during training.
        /// </summary>
        public double QLimit { get; set; }

        /// <summary>
        /// Array of differences between the actual and reconstructed spectra (residuals).
        /// Useful for visualization: allows you to see at which wavelengths the anomaly occurs.
        /// </summary>
        public double[] Residual { get; set; }

        public PcaAnomalyResult(
            bool isAnomaly,
            double t2,
            double q,
            double t2Limit,
            double qLimit,
            double[] residual,
            string message) : base(isAnomaly, message)
        {
            IsAnomaly = isAnomaly;
            T2 = t2;
            Q = q;
            T2Limit = t2Limit;
            QLimit = qLimit;
            Residual = residual;
            Message = message;
        }
    }
}
