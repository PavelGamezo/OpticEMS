using OpticEMS.Contracts.Preprocessing;

namespace OpticEMS.Preprocessing.Operations.Arithmetic
{
    public abstract class BinaryArithmeticProcessor : INodeProcessor
    {
        public double Process(double[] inputs, double currentTimeMs)
        {
            if (inputs == null || inputs.Length < 2 || inputs[0] == null || inputs[1] == null)
            {
                return 0;
            }

            double signalA = inputs[0];
            double signalB = inputs[1];

            var result = ExecuteOperation(signalA, signalB);

            return result;
        }

        protected abstract double ExecuteOperation(double a, double b);

        public virtual void Reset()
        {

        }
    }
}
