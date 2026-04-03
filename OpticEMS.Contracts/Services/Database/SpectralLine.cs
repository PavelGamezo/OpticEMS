namespace OpticEMS.Contracts.Services.Database
{
    public class SpectralLine
    {
        public int Id { get; set; }

        public string Element { get; set; }

        public double Wavelength { get; set; }

        public string Ionization { get; set; }

        public string ColorHex { get; set; } = "#3498DB";
    }
}
