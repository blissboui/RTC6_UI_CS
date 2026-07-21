using System;
using System.Collections.Generic;

namespace RTC6_UI.Rtc6sdk.Dxf
{
    /// <summary>
    /// DXF 로드 및 좌표 변환에 사용되는 설정값을 저장합니다.
    /// 단위 변환, 배율, 회전, 반전, 오프셋, 곡선 정밀도 및 필터링 조건을 관리합니다.
    /// </summary>
    public sealed class DxfLoadOptions
    {
        // DXF 원본 좌표를 mm로 변환하는 배율입니다.
        // mm=1.0, inch=25.4
        public double SourceUnitToMillimeter { get; init; } = 1.0;

        // mm 변환 후 추가로 적용할 전체 배율입니다.
        public double Scale { get; init; } = 1.0;

        public double RotationDegrees { get; init; }

        public bool MirrorX { get; init; }

        public bool MirrorY { get; init; }

        public double OffsetXMillimeter { get; init; }

        public double OffsetYMillimeter { get; init; }

        // 원·원호·타원을 직선으로 분할할 때 사용할 정밀도입니다.
        public int CurvePrecision { get; init; } = 128;

        // Spline과 3D Polyline을 점열로 변환할 때 사용할 정밀도입니다.
        public int SplinePrecision { get; init; } = 128;

        // 연속 점을 같은 점으로 판단하는 허용 오차(mm)입니다.
        public double WeldToleranceMillimeter { get; init; } = 0.0001;

        // 2D 가공으로 허용할 Z 높이 오차(mm)입니다.
        public double ZToleranceMillimeter { get; init; } = 0.001;

        // true이면 Z 허용 오차를 넘는 도형을 오류로 처리합니다.
        public bool RejectNonPlanarEntities { get; init; } = true;

        public bool IgnoreInvisibleEntities { get; init; } = true;

        public bool IgnoreInvisibleLayers { get; init; } = true;

        public bool IgnoreFrozenLayers { get; init; } = true;

        public bool IgnoreNonPlotLayers { get; init; } = false;

        // INSERT(BLOCK 참조)를 실제 Entity로 전개합니다.
        public bool ExplodeInserts { get; init; } = true;

        // 비어 있으면 모든 레이어를 허용합니다.
        public HashSet<string> IncludedLayers { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> ExcludedLayers { get; init; } =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "DEFPOINTS"
            };

        public int MaximumEntityCount { get; init; } = 500_000;

        public int MaximumPointCount { get; init; } = 2_000_000;

        public int MaximumInsertDepth { get; init; } = 16;

        public bool RemoveZeroLengthContours { get; init; } = true;

        // 레이어별 공정 순서가 중요하면 false로 유지하세요.
        public bool OptimizeTravelOrder { get; init; } = false;

        public bool AllowReverseOpenContours { get; init; } = true;
    }

}
