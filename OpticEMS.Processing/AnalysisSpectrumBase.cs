namespace OpticEMS.Processing
{
    public abstract class AnalysisSpectrumBase
    {
        public string Name { get; protected set; }

        public bool IsTrained { get; protected set; }

        /// <summary>
        /// The main method for working in real time. 
        /// Takes the raw data from the spectrometer(an array of intensities) and turns it 
        /// into a single AnomalyResult that describes the state of the process at that particular moment.
        /// </summary>
        /// <param name="intensities"></param>
        /// <returns></returns>
        public abstract Result Analyze(double[] intensities);

        /// <summary>
        /// Method for training (accepts a collection of spectra)
        /// </summary>
        /// <param name="trainingData"></param>
        public abstract void Train(IEnumerable<double[]> trainingData);

        /// <summary>
        /// Method for working with analysis files
        /// </summary>
        /// <param name="filePath"></param>
        public abstract void SaveModel(string filePath);
        public abstract void LoadModel(string filePath);
    }
}
