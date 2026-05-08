namespace OpticEMS.Services.Validators
{
    public interface IExpressionValidator
    {
        ValidationResult Validate(string expression, List<string> channelNames);
    }
}
