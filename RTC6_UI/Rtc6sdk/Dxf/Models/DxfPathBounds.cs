namespace RTC6_UI.Rtc6sdk.Dxf.Models
{
    /// <summary>
    /// 변환된 DXF 도형 전체의 최소·최대 좌표와 가로·세로 크기를 저장합니다.
    /// 모든 값은 mm 단위입니다.
    /// </summary>
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
