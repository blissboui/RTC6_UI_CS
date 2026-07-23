using System.Collections.Generic;
using netDxf.Header;
using RTC6_UI.Dxf.Models;

namespace RTC6_UI.Dxf
{
    /// <summary>
    /// DXF 파일 처리 결과를 저장합니다.
    /// 변환된 Contour, Jump/Mark 명령, 경계 정보, 통계, 경고 및 오류 정보를 포함합니다.
    /// </summary>
    public sealed class DxfLoadResult
    {
        public bool Success { get; internal set; }

        public string ErrorMessage { get; internal set; } = string.Empty;

        public string FilePath { get; internal set; } = string.Empty;

        public DxfVersion Version { get; internal set; }

        public string SourceDrawingUnit { get; internal set; } = string.Empty;

        public List<DxfContour> Contours { get; internal set; } = new();

        public List<DxfMotionCommand> Commands { get; internal set; } = new();

        public List<string> Warnings { get; } = new();

        public DxfLoadStatistics Statistics { get; } = new();

        public DxfPathBounds? Bounds { get; internal set; }

        public bool HasWarnings => Warnings.Count > 0;
    }
}
