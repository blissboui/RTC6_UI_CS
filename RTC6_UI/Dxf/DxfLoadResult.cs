using System.Collections.Generic;
using netDxf.Header;
using RTC6_UI.Dxf.Models;

namespace RTC6_UI.Dxf
{
    public sealed class DxfLoadResult
    {
        public bool Success { get; internal set; }

        public string ErrorMessage { get; internal set; } = string.Empty;

        public string FilePath { get; internal set; } = string.Empty;

        public DxfVersion Version { get; internal set; }

        public string SourceDrawingUnit { get; internal set; } = string.Empty;

        public List<DxfContour> Contours { get; internal set; } = new();

        // RTC6 List 작성 전 단계의 Jump/Mark 명령입니다.
        // 좌표 단위는 mm이며 RTC6 정수 좌표가 아닙니다.
        public List<DxfMotionCommand> Commands { get; internal set; } = new();

        public List<string> Warnings { get; } = new();

        public DxfLoadStatistics Statistics { get; } = new();

        public DxfPathBounds? Bounds { get; internal set; }

        public bool HasWarnings => Warnings.Count > 0;
    }
}
