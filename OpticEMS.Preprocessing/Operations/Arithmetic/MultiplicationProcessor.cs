namespace OpticEMS.Preprocessing.Operations.Arithmetic
{
    public class MultiplicationProcessor : BinaryArithmeticProcessor
    {
        protected override double ExecuteOperation(double a, double b)
        {
            return a * b;
        }
    }
}
