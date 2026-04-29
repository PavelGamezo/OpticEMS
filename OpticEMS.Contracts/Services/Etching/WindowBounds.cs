using System;
using System.Collections.Generic;
using System.Text;

namespace OpticEMS.Services.Etching
{
    public class WindowBounds
    {
        public int WavelengthIndex { get; set; }

        // Координаты по оси времени (X)
        public double StartTime { get; set; } // В секундах
        public double EndTime { get; set; }   // StartTime + WindowWidth

        // Координаты по оси интенсивности (Y)
        public double Top { get; set; }       // Reference + WindowHeight
        public double Bottom { get; set; }    // Reference - WindowHeight
        public double Reference { get; set; } // Центральная линия окна

        // Цвет для отрисовки (можно привязать к цвету линии графика)
        public string ColorHex { get; set; }
    }
}
