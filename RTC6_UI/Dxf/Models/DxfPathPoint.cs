using System;

namespace RTC6_UI.Dxf.Models
{
    public readonly record struct DxfPathPoint(double X, double Y)
    {
        public double DistanceTo(DxfPathPoint other)
        {
            double dx = other.X - X;
            double dy = other.Y - Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
