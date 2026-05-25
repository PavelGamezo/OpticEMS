namespace OpticEMS.Preprocessing.Operations.Arithmetic
{
    public class SubtractionProcessor : BinaryArithmeticProcessor
    {
        protected override double ExecuteOperation(double a, double b)
        {
            return a - b;
        }
    }
}
