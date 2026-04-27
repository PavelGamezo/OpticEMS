namespace OpticEMS.Processing.PCA
{
    public class PcaAnalysisHandler : AnalysisHandlerBase<PcaSpectrumAnalyzer>
    {
        private readonly string _modelPath;
        private readonly int _components;
        private readonly int _minTrainingSize;
        private readonly List<uint[]> _trainingBuffer = new();

        // STATE
        private bool _isBusy = false;
        private bool _modelLoaded = false;

        public string Status { get; private set; } = "PCA not trained";

        public PcaAnalysisHandler(
            PcaSpectrumAnalyzer analyzer,
            string recipeName,
            int components,
            int minTrainingSize = 15) : base(analyzer)
        {
            _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", $"{recipeName}.pca");
            _components = components;
            _minTrainingSize = minTrainingSize;

            Analyzer.NComponents = _components;

            TryLoadExistingModel();
        }

        public override async Task<Result> ProcessAsync(uint[] spectrum)
        {
            if (_isBusy)
            {
                return new Result(false, "PCA busy");
            }

            if (!Analyzer.IsTrained)
            {
                Status = "PCA not trained";
                return new Result(false, Status);
            }

            _isBusy = true;

            try
            {
                var copy = spectrum.ToArray();

                var result = await Task.Run(() => Analyzer.Analyze(copy));

                Status = result.IsAnomaly
                    ? $"PCA ANOMALY → {result.Message}"
                    : $"PCA Normal | {result.Message}";

                return result;
            }
            catch (Exception ex)
            {
                Status = "PCA Error";
                return new Result(false, ex.Message);
            }
            finally
            {
                _isBusy = false; 
            }
        }

        public Result TryAutoTrain(IEnumerable<uint[]> spectra)
        {
            try
            {
                var data = spectra.Select(s => s.ToArray()).ToArray();

                if (data.Length < _minTrainingSize)
                {
                    Status = $"Not enough data for PCA training (need ≥ {_minTrainingSize}, got {data.Length})";
                    return new Result(false, Status);
                }

                Analyzer.Train(data);
                Analyzer.SaveModel(_modelPath);

                Status = "PCA trained";
                return new Result(true, Status);
            }
            catch (Exception ex)
            {
                Status = "PCA Error";
                return new Result(false, ex.Message);
            }
        }

        public void PushForTraining(uint[] spectrum)
        {
            _trainingBuffer.Add(spectrum.ToArray());

            if (_trainingBuffer.Count > _minTrainingSize * 2)
            {
                _trainingBuffer.RemoveRange(0, _trainingBuffer.Count - _minTrainingSize * 2);
            }
        }

        public Result TrainFromBuffer()
        {
            return TryAutoTrain(_trainingBuffer);
        }

        public override Result Process(uint[] currentSpectrum)
        {
            if (!Analyzer.IsTrained)
            {
                Status = "PCA not trained";
                return new Result(false, Status);
            }

            try
            {
                var copy = currentSpectrum.ToArray();
                var result = Analyzer.Analyze(copy);

                Status = result.IsAnomaly
                    ? $"PCA ANOMALY → {result.Message}"
                    : $"PCA Normal | {result.Message}";

                return new Result(true, Status);
            }
            catch (Exception ex)
            {
                Status = "PCA Error";
                return new Result(false, ex.Message);
            }
        }

        private void TryLoadExistingModel()
        {
            if (!File.Exists(_modelPath))
            {
                return;
            }

            try
            {
                Analyzer.LoadModel(_modelPath);
                _modelLoaded = true;
                Status = "PCA model loaded";
            }
            catch
            {
                Status = "PCA model load error";
            }
        }
    }
}
