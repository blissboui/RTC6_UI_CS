using netDxf;
using netDxf.Header;
using RTC6_UI.Dxf.Internal;
using RTC6_UI.Dxf.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



// 파일: DxfLoader.cs
// 대상: .NET 8 WPF + netDxf 2023.11.10
//
// 역할:
// 1. DXF 파일 검증 및 로드
// 2. 레이저 가공에 필요한 2D 경로로 정규화
// 3. LINE / ARC / CIRCLE / ELLIPSE / POLYLINE / SPLINE 지원
// 4. INSERT(BLOCK 참조) 전개 지원
// 5. 레이어/가시성 필터, 단위/회전/반전/이동 변환 지원
// 6. 곡선을 직선 점열로 근사
// 7. 경로 통계, 경고, Bounds 생성
// 8. RTC6 List 작성용 Jump/Mark 중간 명령 자동 생성
//
// 중요:
// - 결과의 Commands는 mm 단위입니다.
// - 실제 RTC6 정수 좌표 변환과 List 기록은 Rtc6Controller에서 담당하세요.
// - 레이저 출력, 인터록, 레이저 파라미터는 이 클래스에서 제어하지 않습니다.
// ============================================================

namespace RTC6_UI.Dxf
{
    /// <summary>
    /// DXF 파일 로드 과정을 총괄하는 클래스입니다.
    /// 파일 검증, Entity 추출, Contour 정리, 이동 명령 생성 및 통계 계산을 순서대로 수행합니다.
    /// </summary>
    public sealed class DxfLoader
    { 
        private readonly DxfEntityExtractor _entityExtractor;
        private readonly DxfContourProcessor _contourProcessor;
        private readonly DxfPathOptimizer _pathOptimizer;
        private readonly DxfMotionCommandBuilder _motionCommandBuilder;
        private readonly DxfResultCalculator _resultCalculator;

        public DxfLoader()
        {
            DxfCoordinateTransformer coordinateTransformer = new();
            _contourProcessor = new DxfContourProcessor();
            _resultCalculator = new DxfResultCalculator();

            DxfGeometryConverter geometryConverter = new(
                coordinateTransformer,
                _contourProcessor
            );

            _entityExtractor = new DxfEntityExtractor(
                geometryConverter,
                _resultCalculator
            );

            _pathOptimizer = new DxfPathOptimizer();
            _motionCommandBuilder = new DxfMotionCommandBuilder();
        }

        // WPF UI가 멈추지 않도록 백그라운드에서 로드합니다.
        public Task<DxfLoadResult> LoadAsync(
            string filePath,
            DxfLoadOptions? options = null,
            IProgress<DxfLoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(
                () => Load(
                    filePath,
                    options,
                    progress,
                    cancellationToken
                ),
                cancellationToken
            );
        }

        public DxfLoadResult Load(
            string filePath,
            DxfLoadOptions? options = null,
            IProgress<DxfLoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            DxfLoadOptions actualOptions = options ?? new DxfLoadOptions();

            DxfLoadResult result = new()
            {
                FilePath = filePath
            };

            if (!DxfValidator.ValidateOptions(actualOptions, out string optionError))
            {
                return Fail(result, optionError);
            }

            if (!DxfValidator.ValidateFilePath(filePath, out string fileError))
            {
                return Fail(result, fileError);
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();   // 작업 취소 시 예외 발생

                DxfVersion version = DxfDocument.CheckDxfFileVersion(filePath);

                result.Version = version;

                if (version < DxfVersion.AutoCad2000)
                {
                    return Fail(
                        result,
                        "지원하지 않는 DXF 버전입니다.\n" +
                        $"버전: {version}"
                    );
                }

                DxfDocument? document = DxfDocument.Load(filePath);

                if (document is null)
                {
                    return Fail(
                        result,
                        "DXF 파일을 읽지 못했습니다."
                    );
                }

                result.SourceDrawingUnit =
                    document.DrawingVariables
                        .InsUnits
                        .ToString();

                DxfExtractionContext context =
                    _entityExtractor.Extract(
                        document,
                        actualOptions,
                        result,
                        progress,
                        cancellationToken
                    );

                List<DxfContour> contours =
                    _contourProcessor.FinalizeContours(
                        context.Contours,
                        actualOptions
                    );

                if (actualOptions.OptimizeTravelOrder)
                {
                    contours =
                        _pathOptimizer.OptimizeTravelOrder(
                            contours,
                            actualOptions
                                .AllowReverseOpenContours
                        );
                }

                result.Contours = contours;

                result.Commands =
                    _motionCommandBuilder.BuildMotionCommands(
                        contours
                    );

                result.Bounds =
                    _resultCalculator.CalculateBounds(
                        contours
                    );

                _resultCalculator.FillFinalStatistics(result);

                foreach (
                    KeyValuePair<string, int> unsupported
                    in result.Statistics.UnsupportedEntityCounts)
                {
                    result.Warnings.Add(
                        "미지원 Entity 건너뜀: " +
                        $"{unsupported.Key} " +
                        $"{unsupported.Value}개"
                    );
                }

                if (contours.Count == 0)
                {
                    return Fail(
                        result,
                        "가공 가능한 DXF 경로를 찾지 못했습니다."
                    );
                }

                result.Success = true;
                return result;
            }
            catch (OperationCanceledException)
            {
                return Fail(
                    result,
                    "DXF 로드가 취소되었습니다."
                );
            }
            catch (Exception exception)
            {
                return Fail(
                    result,
                    BuildRootExceptionMessage(
                        "DXF 파싱 중 오류가 발생했습니다.",
                        exception
                    )
                );
            }
        }

        private static DxfLoadResult Fail(
            DxfLoadResult result,
            string message)
        {
            result.Success = false;
            result.ErrorMessage = message;

            return result;
        }

        private static string BuildRootExceptionMessage(
            string title,
            Exception exception)
        {
            Exception root = exception;

            while (root.InnerException is not null)
            {
                root = root.InnerException;
            }

            return
                $"{title}\n" +
                $"종류: {root.GetType().Name}\n" +
                $"내용: {root.Message}";
        }
    }

}

