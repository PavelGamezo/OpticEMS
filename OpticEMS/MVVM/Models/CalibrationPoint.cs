namespace OpticEMS.MVVM.Models
{
    public class CalibrationPoint
    {
        public double Pixel { get; set; }

        public double Wavelength { get; set; }

        public CalibrationPoint(double pixel, double wavelength)
        {
            Pixel = pixel;
            Wavelength = wavelength;
        }
    }
}
