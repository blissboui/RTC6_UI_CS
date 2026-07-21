using System;
using System.Collections.Generic;

namespace RTC6_UI.Rtc6sdk.Dxf.Models
{
    /// <summary>
    /// DXF 처리 과정에서 계산된 통계 정보를 저장합니다.
    /// Entity, Contour, Point, Command 개수와 Mark 및 Jump 이동 거리를 포함합니다.
    /// </summary>
    public sealed class DxfLoadStatistics
    {
        public int TotalEntityCount { get; internal set; }

        public int SupportedEntityCount { get; internal set; }

        public int SkippedEntityCount { get; internal set; }

        public int InsertCount { get; internal set; }

        public int ContourCount { get; internal set; }

        public int PointCount { get; internal set; }

        public double TotalMarkLengthMillimeter { get; internal set; }

        public double EstimatedJumpLengthMillimeter { get; internal set; }

        public Dictionary<string, int> EntityCounts { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, int> UnsupportedEntityCounts { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

}
