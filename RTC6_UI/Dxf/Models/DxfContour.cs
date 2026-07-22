using System.Collections.Generic;

namespace RTC6_UI.Dxf.Models
{
    /// <summary>
    /// 서로 연속해서 연결된 하나의 DXF 경로를 나타냅니다.
    /// 경로를 구성하는 점 목록, 레이어 이름 및 닫힌 경로 여부를 저장합니다.
    /// </summary>
    public sealed class DxfContour  // 윤곽선
    {
        public string LayerName { get; init; } = "0";

        public string SourceEntityType { get; init; } = string.Empty;

        public string? SourceHandle { get; init; }

        public bool IsClosed { get; internal set; }

        public List<DxfPathPoint> Points { get; internal set; } = new();

        public DxfPathPoint StartPoint => Points[0];

        public DxfPathPoint EndPoint =>
            IsClosed ? Points[0] : Points[^1];

        public double MarkLength
        {
            get
            {
                if (Points.Count < 2)
                {
                    return 0.0;
                }

                double length = 0.0;

                for (int index = 1; index < Points.Count; index++)
                {
                    length += Points[index - 1].DistanceTo(Points[index]);
                }

                if (IsClosed)
                {
                    length += Points[^1].DistanceTo(Points[0]);
                }

                return length;
            }
        }

        public DxfContour ReverseOpenContour()
        {
            if (IsClosed)
            {
                return this;
            }

            List<DxfPathPoint> reversed = new(Points);
            reversed.Reverse();

            return new DxfContour
            {
                LayerName = LayerName,
                SourceEntityType = SourceEntityType,
                SourceHandle = SourceHandle,
                IsClosed = false,
                Points = reversed
            };
        }
    }
}
