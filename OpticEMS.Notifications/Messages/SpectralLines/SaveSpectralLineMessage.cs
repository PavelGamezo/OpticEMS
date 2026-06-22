using OpticEMS.Contracts.Services.Recipe;

namespace OpticEMS.Notifications.Messages.SpectralLines
{
    public class SaveSpectralLineMessage
    {
        public string Element { get; }
        public string WavelengthText { get; }
        public string HexColor { get; }

        public SaveSpectralLineMessage(string element, string wavelengthText, string hexColor)
        {
            Element = element;
            WavelengthText = wavelengthText;
            HexColor = hexColor;
        }
    }
}
