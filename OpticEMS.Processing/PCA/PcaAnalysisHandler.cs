using Serilog;

namespace OpticEMS.Processing.PCA
{
    public class PcaAnalysisHandler : AnalysisHandlerBase<PcaSpectrumAnalyzer>
    {
        private readonly string _modelPath;
        private readonly int _components;
        private readonly int _minTrainingSize;
        private readonly List<double[]> _trainingBuffer = new();

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
            _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                "Models", 
                $"{recipeName}.pca");
            _components = components;
            _minTrainingSize = minTrainingSize;

            Analyzer.NComponents = _components;

            TryLoadExistingModel();
            Log.Information("[PROCESSING]: PCA processing handler compiled");
        }

        public override async Task<Result> ProcessAsync(double[] spectrum)
        {
            if (_isBusy)
            {
                return new PcaAnomalyResult(false, 0, 0, 0, 0, null, "PCA busy");
            }

            if (!Analyzer.IsTrained)
            {
                Status = "PCA not trained";
                return new PcaAnomalyResult(false, 0, 0, 0, 0, null, Status);
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
                return new PcaAnomalyResult(false, 0, 0, 0, 0, null, ex.Message);
            }
            finally
            {
                _isBusy = false; 
            }
        }

        public List<(int start, int end)> DetectAnomalyRanges(double[] residual, double k = 3.0)
        {
            var ranges = new List<(int start, int end)>();

            if (residual == null || residual.Length == 0)
                return ranges;

            double[] abs = new double[residual.Length];
            for (int i = 0; i < residual.Length; i++)
                abs[i] = Math.Abs(residual[i]);

            double sum = 0;
            for (int i = 0; i < abs.Length; i++)
                sum += abs[i];
            double mean = sum / abs.Length;

            double var = 0;
            for (int i = 0; i < abs.Length; i++)
            {
                double d = abs[i] - mean;
                var += d * d;
            }
            double std = Math.Sqrt(var / abs.Length);

            if (std == 0)
                return ranges;

            double threshold = mean + k * std;

            int start = -1;

            for (int i = 0; i < abs.Length; i++)
            {
                bool isAnomaly = abs[i] > threshold;

                if (isAnomaly)
                {
                    if (start == -1)
                        start = i;
                }
                else
                {
                    if (start != -1)
                    {
                        ranges.Add((start, i - 1));
                        start = -1;
                    }
                }
            }

            if (start != -1)
                ranges.Add((start, abs.Length - 1));

            return ranges;
        }

        public Result TryAutoTrain(IEnumerable<double[]> spectra)
        {
            try
            {
                var data = spectra.Select(s => s.ToArray()).ToArray();

                Log.Information("[PROCESSING]: Starting PCA training. Samples: {Samples}, Features: {Features}, Target Components: {Components}",
                    data.Length, data.Length > 0 ? data[0].Length : 0, _components);

                if (data.Length < _minTrainingSize)
                {
                    Log.Warning("[PROCESSING]: PCA training aborted: insufficient data ({Samples} < {MinSize})", 
                        data.Length, 
                        _minTrainingSize);

                    Status = $"Not enough data for PCA training";
                    return new Result(false, Status);
                }

                Analyzer.Train(data);
                Analyzer.SaveModel(_modelPath);

                Log.Information("[PROCESSING]: PCA model successfully trained and saved to {Path}", _modelPath);

                Status = "PCA trained";
                return new Result(true, Status);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "[PROCESSING]: PCA error during training");

                Status = "PCA Error";
                return new Result(false, exception.Message);
            }
        }

        public void PushForTraining(double[] spectrum)
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

        public override Result Process(double[] currentSpectrum)
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
            catch (Exception exception)
            {
                Log.Error(exception, "[PROCESSING]: PCA error during training");

                Status = "PCA Error";
                return new Result(false, exception.Message);
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
                Log.Information("[PROCESSING]: PCA model loaded");

            }
            catch (Exception exception)
            {
                Status = "PCA model load error";
                Log.Error(exception, "[PROCESSING]: PCA error during training");
            }
        }
    }
}
