namespace OpticEMS.Preprocessing.Operations.Arithmetic
{
    public class AdditionProcessor : BinaryArithmeticProcessor
    {
        protected override double ExecuteOperation(double a, double b) => a + b;
    }
}
