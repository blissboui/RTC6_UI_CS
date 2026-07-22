using System.Collections.Generic;
using RTC6_UI.Dxf;
using RTC6_UI.Dxf.Models;

namespace RTC6_UI.Dxf.Internal
{
    /// <summary>
    /// DXF Entity 추출 과정에서 여러 처리 클래스가 공유하는 임시 작업 데이터를 저장합니다.
    /// 로드 옵션, 처리 결과, 추출된 Contour 및 현재 처리 상태를 관리합니다.
    /// </summary>
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
