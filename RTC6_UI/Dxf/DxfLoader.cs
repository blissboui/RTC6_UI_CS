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

        /// <summary>
        /// DXF 파일을 로드하고 가공 가능한 mm 단위 경로와 Jump/Mark 명령으로 변환합니다.
        /// 파일 및 옵션 검증, DXF 버전 확인, Entity 추출, Contour 정리,
        /// 이동 순서 최적화, 명령 생성, 경계 계산 및 통계 집계를 순서대로 수행합니다.
        /// </summary>
        /// <param name="filePath">로드할 DXF 파일의 전체 경로입니다.</param>
        /// <param name="options">
        /// 단위 변환, 배율, 회전, 반전, 오프셋, 정밀도 및 필터링 설정입니다.
        /// null이면 기본 설정을 사용합니다.
        /// </param>
        /// <param name="progress">Entity 처리 진행 상태를 전달받는 객체입니다.</param>
        /// <param name="cancellationToken">DXF 로드 작업의 취소 요청을 확인하는 토큰입니다.</param>
        /// <returns>
        /// 성공 여부, Contour, 이동 명령, 경계, 통계, 경고 및 오류 정보를 포함한 결과입니다.
        /// </returns>
        public DxfLoadResult Load(
            string filePath,
            DxfLoadOptions? options = null,
            IProgress<DxfLoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // 전달된 옵션이 없으면 기본 옵션 사용
            DxfLoadOptions actualOptions = options ?? new DxfLoadOptions();

            // 파일 경로를 포함한 결과 객체 생성
            DxfLoadResult result = new()
            {
                FilePath = filePath
            };

            // 좌표 변환 및 필터링 옵션 유효성 검사
            if (!DxfValidator.ValidateOptions(actualOptions, out string optionError))
                return Fail(result, optionError);

            // 파일 존재 여부와 DXF 확장자 검사
            if (!DxfValidator.ValidateFilePath(filePath, out string fileError))
                return Fail(result, fileError);

            try
            {
                // 본격적인 처리 전에 취소 요청 확인
                cancellationToken.ThrowIfCancellationRequested();

                // DXF 파일 버전 확인
                DxfVersion version = DxfDocument.CheckDxfFileVersion(filePath);
                result.Version = version;

                // AutoCAD 2000 미만 버전은 지원하지 않음
                if (version < DxfVersion.AutoCad2000)
                {
                    return Fail(
                        result,
                        "지원하지 않는 DXF 버전입니다.\n" +
                        $"버전: {version}");
                }

                // DXF 문서 로드
                DxfDocument? document = DxfDocument.Load(filePath);

                if (document is null)
                    return Fail(result, "DXF 파일을 읽지 못했습니다.");

                // DXF 원본 도면 단위 저장
                result.SourceDrawingUnit = document.DrawingVariables.InsUnits.ToString();

                // Entity를 순회하며 mm 단위 Contour 추출
                DxfExtractionContext context = _entityExtractor.Extract(
                    document,
                    actualOptions,
                    result,
                    progress,
                    cancellationToken);

                // 잘못된 경로 제거, 연속 Contour 병합 및 닫힌 경로 정리
                List<DxfContour> contours = _contourProcessor.FinalizeContours(context.Contours, actualOptions);

                // 설정된 경우 Contour 간 Jump 이동 거리가 줄어들도록 순서 최적화
                if (actualOptions.OptimizeTravelOrder)
                {
                    contours = _pathOptimizer.OptimizeTravelOrder(contours, actualOptions.AllowReverseOpenContours);
                }

                // 최종 mm 단위 Contour 저장
                result.Contours = contours;

                // Contour를 mm 단위 Jump/Mark 명령으로 변환
                result.Commands = _motionCommandBuilder.BuildMotionCommands(contours);

                // 전체 가공 경로의 최소·최대 좌표와 크기 계산
                result.Bounds = _resultCalculator.CalculateBounds(contours);

                // Entity, Contour, Point, Jump/Mark 거리 등의 최종 통계 계산
                _resultCalculator.FillFinalStatistics(result);

                // 미지원 Entity 정보를 사용자 경고 목록에 추가
                foreach (KeyValuePair<string, int> unsupported in result.Statistics.UnsupportedEntityCounts)
                {
                    result.Warnings.Add(
                        "미지원 Entity 건너뜀: " +
                        $"{unsupported.Key} " +
                        $"{unsupported.Value}개");
                }

                // 최종적으로 가공 가능한 경로가 없으면 실패 처리
                if (contours.Count == 0)
                    return Fail(result, "가공 가능한 DXF 경로를 찾지 못했습니다.");

                // 모든 처리 완료
                result.Success = true;
                return result;
            }
            catch (OperationCanceledException)
            {
                // CancellationToken을 통한 정상적인 작업 취소 처리
                return Fail(result, "DXF 로드가 취소되었습니다.");
            }
            catch (Exception exception)
            {
                // 라이브러리 및 내부 처리 중 발생한 예외를 사용자 메시지로 변환
                return Fail(
                    result,
                    BuildRootExceptionMessage(
                        "DXF 파싱 중 오류가 발생했습니다.",
                        exception));
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

