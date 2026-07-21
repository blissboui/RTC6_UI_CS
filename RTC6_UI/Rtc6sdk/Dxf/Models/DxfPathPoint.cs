using System;

namespace RTC6_UI.Rtc6sdk.Dxf.Models
{
    /// <summary>
    /// 단위, 배율, 반전, 회전 및 오프셋이 적용된 mm 단위의 2차원 좌표를 나타냅니다.
    /// </summary>
    public readonly record struct DxfPathPoint(double X, double Y)  // 점 좌표
    {
        public double DistanceTo(DxfPathPoint other)
        {   // 다른 점까지의 거리 반환
            double dx = other.X - X;
            double dy = other.Y - Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
