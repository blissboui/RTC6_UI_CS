using System.Collections.Generic;

namespace RTC6_UI.Dxf.Models
{
    public sealed class DxfContour
    {
        public string LayerName { get; init; } = "0";

        public string SourceEntityType { get; init; } = string.Empty;

        public string? SourceHandle { get; init; }

        public bool IsClosed { get; init; }

        public List<DxfPathPoint> Points { get; init; } = new();

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
                    length += Points[index - 1]
                        .DistanceTo(Points[index]);
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
