namespace OpticEMS.Preprocessing.Operations.Arithmetic
{
    public class DivisionProcessor : BinaryArithmeticProcessor
    {
        protected override double ExecuteOperation(double a, double b)
        {
            if (Math.Abs(b) < 1e-9)
            {
                return 0.0;
            }

            return a / b;
        }
    }
}
