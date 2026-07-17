
namespace RTC6_UI.Dxf.Models
{
    public readonly record struct DxfPathBounds(
        double MinimumX,
        double MinimumY,
        double MaximumX,
        double MaximumY)
    {
        public double Width => MaximumX - MinimumX;

        public double Height => MaximumY - MinimumY;

        public DxfPathPoint Center => new(
            (MinimumX + MaximumX) * 0.5,
            (MinimumY + MaximumY) * 0.5
        );
    }
}
