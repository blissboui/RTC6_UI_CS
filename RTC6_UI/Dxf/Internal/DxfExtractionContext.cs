using System.Collections.Generic;
using RTC6_UI.Dxf.Models;

namespace RTC6_UI.Dxf.Internal
{
    internal sealed class DxfExtractionContext
    {
        public DxfExtractionContext(
            DxfLoadOptions options,
            DxfLoadResult result)
        {
            Options = options;
            Result = result;
        }

        public DxfLoadOptions Options { get; }

        public DxfLoadResult Result { get; }

        public List<DxfContour> Contours { get; } = new();

        public int TotalPointCount { get; set; }
    }
}
