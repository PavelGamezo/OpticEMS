using System;
using System.Collections.Generic;
using System.Text;

namespace OpticEMS.Services.Etching
{
    public class WindowBounds
    {
        public int WavelengthIndex { get; set; }

        public double StartTime { get; set; }
        public double EndTime { get; set; }

        public double Top { get; set; }
        public double Bottom { get; set; }
        public double Reference { get; set; }

        public string ColorHex { get; set; }
    }
}
