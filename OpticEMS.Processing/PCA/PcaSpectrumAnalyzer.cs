using MathNet.Numerics.LinearAlgebra;
using System.Text.Json;

namespace OpticEMS.Processing.PCA
{
    public class PcaSpectrumAnalyzer : AnalysisSpectrumBase
    {
        private Vector<double> _mean = Vector<double>.Build.Dense(0);
        private Matrix<double> _loadings = Matrix<double>.Build.Dense(0, 0);
        private Vector<double> _eigenvalues = Vector<double>.Build.Dense(0);

        private double _t2Limit = 0;
        private double _qLimit = 0;
        private int _nComponents = 5;

        public PcaSpectrumAnalyzer() => Name = "Principal Component Analysis (PCA)";

        public int NComponents
        {
            get => _nComponents;
            set => _nComponents = Math.Clamp(value, 1, 15);
        }

        public bool TryAutoTrain(IEnumerable<uint[]> spectra, string modelPath)
        {
            if (IsTrained || spectra.Count() < 30)
            {
                return false;
            }

            try
            {
                Train(spectra);
                SaveModel(modelPath);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public override double Predict(uint[] intensities)
        {
            var result = Analyze(intensities);
            return result.Q;
        }

        /// <summary>
        /// Main PCA analize method for spectrum process
        /// </summary>
        public PcaAnomalyResult Analyze(uint[] intensities)
        {
            if (!IsTrained || _mean.Count == 0)
            {
                return new PcaAnomalyResult { IsAnomaly = false, Message = "PCA model is not trained" };
            }

            if (intensities.Length != _mean.Count)
            {
                return new PcaAnomalyResult { IsAnomaly = false, Message = "Spectrum length mismatch" };
            }

            if (_loadings.RowCount != intensities.Length)
            {
                return new PcaAnomalyResult
                {
                    IsAnomaly = false,
                    Message = $"Dimension mismatch: loadings rows={_loadings.RowCount}, spectrum={intensities.Length}"
                };
            }
            
            var xCentered = Vector<double>.Build.Dense(intensities.Length);
            for (int i = 0; i < intensities.Length; i++)
            {
                xCentered[i] = intensities[i] - _mean[i];
            }

            // Scores
            var t = xCentered * _loadings;

            // T²
            var t2 = t.PointwiseDivide(_eigenvalues).PointwisePower(2).Sum();

            // Q statistic (SPE)
            var xRecon = t * _loadings.Transpose();
            var residual = xCentered - xRecon;
            var q = residual.PointwisePower(2).Sum();

            bool isAnomaly = t2 > _t2Limit || q > _qLimit;

            return new PcaAnomalyResult
            {
                IsAnomaly = isAnomaly,
                T2 = t2,
                Q = q,
                T2Limit = _t2Limit,
                QLimit = _qLimit,
                Residual = residual.ToArray(),
                Message = $"T²={t2:F15} | Q={q:F5}"
            };
        }

        public override void Train(IEnumerable<uint[]> trainingData)
        {
            var doubleData = trainingData.Select(arr => arr.Select(x => (double)x).ToArray()).ToArray();

            var matrix = Matrix<double>.Build.DenseOfRowArrays(doubleData);

            if (matrix.RowCount < 5)
                throw new ArgumentException($"Для обучения PCA нужно минимум 5 спектров. Сейчас: {matrix.RowCount}");

            int maxComponents = Math.Min(_nComponents, Math.Min(matrix.RowCount - 1, matrix.ColumnCount));

            _mean = matrix.ColumnSums() / matrix.RowCount;

            var centered = matrix - Matrix<double>.Build.Dense(matrix.RowCount, matrix.ColumnCount, (i, j) => _mean[j]);

            var svd = centered.Svd(computeVectors: true);

            _loadings = svd.VT.SubMatrix(0, maxComponents, 0, matrix.ColumnCount).Transpose();

            _eigenvalues = svd.S.SubVector(0, maxComponents).PointwisePower(2);

            CalculateLimits(centered);

            IsTrained = true;
        }

        private void CalculateLimits(Matrix<double> centeredMatrix, double confidence = 0.99)
        {
            var t2Values = new List<double>();
            var qValues = new List<double>();

            foreach (var row in centeredMatrix.EnumerateRows())
            {
                var t = row * _loadings;

                // Защита: добавляем малую константу 1e-9, чтобы не делить на 0
                var t2 = t.PointwiseDivide(_eigenvalues + 1e-9).PointwisePower(2).Sum();

                var recon = t * _loadings.Transpose();
                var q = (row - recon).PointwisePower(2).Sum();

                t2Values.Add(t2);
                qValues.Add(q);
            }

            // Сортируем для нахождения перцентиля
            var sortedT2 = t2Values.OrderBy(x => x).ToList();
            var sortedQ = qValues.OrderBy(x => x).ToList();

            int index = (int)Math.Max(1, t2Values.Count * confidence);

            // Применяем коэффициенты запаса (Safety Factors)
            // 2.0 для T2 и 3.0 для Q — это хорошие стартовые значения для плазмы
            _t2Limit = sortedT2.ElementAtOrDefault(index - 1) * 2.0;
            _qLimit = sortedQ.ElementAtOrDefault(index - 1) * 3.0;

            // "Floor" для лимитов: если при обучении был идеально ровный сигнал, 
            // лимиты не должны быть микроскопическими
            if (_t2Limit < 1e-5) _t2Limit = 0.1;
            if (_qLimit < 1.0) _qLimit = 100.0; // Зависит от среднего шума твоего датчика
        }

        public override void SaveModel(string filePath)
        {
            if (!IsTrained) throw new InvalidOperationException("Model is not trained");

            var modelData = new PcaModel
            {
                Mean = _mean.ToArray(),
                Loadings = _loadings.ToRowMajorArray(),
                Eigenvalues = _eigenvalues.ToArray(),
                T2Limit = _t2Limit,
                QLimit = _qLimit,
                NComponents = _nComponents
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(modelData, options);

            File.WriteAllText(filePath, json);
        }

        public override void LoadModel(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PCA model file not found", filePath);
            }

            string json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<PcaModel>(json);

            if (data == null)
            {
                throw new InvalidOperationException("Invalid model file");
            }

            _mean = Vector<double>.Build.DenseOfArray(data.Mean);
            _eigenvalues = Vector<double>.Build.DenseOfArray(data.Eigenvalues);

            _t2Limit = data.T2Limit;
            _qLimit = data.QLimit;
            _nComponents = data.NComponents;

            int rows = _mean.Count;
            int cols = data.NComponents;

            _loadings = Matrix<double>.Build.DenseOfRowMajor(rows, cols, data.Loadings);

            IsTrained = true;
        }
    }
}
