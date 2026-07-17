using System;
using System.Collections.Generic;

namespace RTC6_UI.Dxf.Models
{
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
