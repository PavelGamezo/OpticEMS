namespace OpticEMS.Processing.PCA
{
    public class PcaModel
    {
        public double[] Mean { get; set; }

        public double[] Loadings { get; set; }
        
        public double[] Eigenvalues { get; set; }
        
        public double T2Limit { get; set; }
        
        public double QLimit { get; set; }
        
        public int NComponents { get; set; }
    }
}
