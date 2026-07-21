using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using netDxf;
using netDxf.Entities;
using RTC6_UI.Rtc6sdk.Dxf;
using RTC6_UI.Rtc6sdk.Dxf.Models;

namespace RTC6_UI.Rtc6sdk.Dxf.Internal
{
    /// <summary>
    /// DXF 문서의 Entity를 순회하고 종류에 맞는 변환 처리를 호출합니다.
    /// 레이어 필터링, 가시성 검사, INSERT 전개 및 지원 Entity 판별을 담당합니다.
    /// </summary>
    internal sealed class DxfEntityExtractor
    {
        private readonly DxfGeometryConverter _geometryConverter;
        private readonly DxfResultCalculator _resultCalculator;

        public DxfEntityExtractor(
            DxfGeometryConverter geometryConverter,
            DxfResultCalculator resultCalculator)
        {
            _geometryConverter = geometryConverter;
            _resultCalculator = resultCalculator;
        }

        public DxfExtractionContext Extract(
            DxfDocument document,
            DxfLoadOptions options,
            DxfLoadResult result,
            IProgress<DxfLoadProgress>? progress,
            CancellationToken cancellationToken)
        {
            List<EntityObject> entities = document.Entities.All.ToList();

            result.Statistics.TotalEntityCount = entities.Count;

            if (entities.Count > options.MaximumEntityCount)
            {
                throw new InvalidDataException(
                    "DXF Entity 개수가 제한을 초과했습니다.\n" +
                    $"Entity 수: {entities.Count}\n" +
                    $"허용 수: {options.MaximumEntityCount}"
                );
            }

            DxfExtractionContext context = new(options, result);

            for (int index = 0; index < entities.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                EntityObject entity = entities[index];

                progress?.Report(
                    new DxfLoadProgress(
                        index + 1,
                        entities.Count,
                        entity.Type.ToString()
                    )
                );

                ProcessEntity(
                    entity,
                    inheritedLayerName: null,
                    insertDepth: 0,
                    context,
                    cancellationToken
                );

                if (context.TotalPointCount > options.MaximumPointCount)
                {
                    throw new InvalidDataException(
                        "DXF 변환 점 개수가 제한을 초과했습니다.\n" +
                        $"현재 점 수: {context.TotalPointCount}\n" +
                        $"허용 점 수: {options.MaximumPointCount}"
                    );
                }
            }

            return context;
        }
        /// <summary>
        /// DXF Entity 하나를 검사하고 Entity 종류에 맞는 변환 처리를 수행합니다.
        /// 가시성, 레이어, 지원 여부를 확인한 뒤 LINE, ARC, CIRCLE, ELLIPSE,
        /// POLYLINE, SPLINE, INSERT 등을 DxfContour로 변환합니다.
        /// </summary>
        /// <param name="entity">현재 처리할 DXF Entity입니다.</param>
        /// <param name="context">
        /// 로드 옵션, 처리 결과, 생성된 Contour 및 통계 정보를 공유하는 작업 컨텍스트입니다.
        /// </param>
        /// <param name="cancellationToken">Entity 처리 취소 요청을 확인하는 토큰입니다.</param>
        private void ProcessEntity(
            EntityObject entity,
            string? inheritedLayerName,
            int insertDepth,
            DxfExtractionContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string layerName =
                ResolveLayerName(
                    entity,
                    inheritedLayerName
                );

            _resultCalculator.CountEntity(
                context.Result.Statistics.EntityCounts,
                entity.Type.ToString()
            );

            if (!ShouldProcessEntity(
                    entity,
                    layerName,
                    context.Options))
            {
                context.Result.Statistics.SkippedEntityCount++;
                return;
            }

            switch (entity)
            {
                case Line line:
                    _geometryConverter.AddLineContour(
                        line,
                        layerName,
                        context
                    );
                    break;

                case Arc arc:
                    _geometryConverter.AddArcContour(
                        arc,
                        layerName,
                        context
                    );
                    break;

                case Circle circle:
                    _geometryConverter.AddCircleContour(
                        circle,
                        layerName,
                        context
                    );
                    break;

                case Ellipse ellipse:
                    _geometryConverter.AddEllipseContour(
                        ellipse,
                        layerName,
                        context
                    );
                    break;

                case Polyline2D polyline2D:
                    _geometryConverter.AddPolyline2DContour(
                        polyline2D,
                        layerName,
                        context
                    );
                    break;

                case Polyline3D polyline3D:
                    _geometryConverter.AddPolyline3DContour(
                        polyline3D,
                        layerName,
                        context
                    );
                    break;

                case Spline spline:
                    _geometryConverter.AddSplineContour(
                        spline,
                        layerName,
                        context
                    );
                    break;

                case Insert insert:
                    ProcessInsert(
                        insert,
                        layerName,
                        insertDepth,
                        context,
                        cancellationToken
                    );
                    break;

                default:
                    _resultCalculator.CountEntity(
                        context.Result
                            .Statistics
                            .UnsupportedEntityCounts,
                        entity.Type.ToString()
                    );

                    context.Result.Statistics.SkippedEntityCount++;
                    break;
            }
        }

        private void ProcessInsert(
            Insert insert,
            string insertLayerName,
            int insertDepth,
            DxfExtractionContext context,
            CancellationToken cancellationToken)
        {
            context.Result.Statistics.InsertCount++;

            if (!context.Options.ExplodeInserts)
            {
                _resultCalculator.CountEntity(
                    context.Result
                        .Statistics
                        .UnsupportedEntityCounts,
                    "Insert"
                );

                context.Result.Statistics.SkippedEntityCount++;
                return;
            }

            if (insertDepth >=
                context.Options.MaximumInsertDepth)
            {
                throw new InvalidDataException(
                    "INSERT 중첩 깊이가 제한을 초과했습니다.\n" +
                    $"허용 깊이: {context.Options.MaximumInsertDepth}"
                );
            }

            List<EntityObject> exploded = insert.Explode();

            foreach (EntityObject child in exploded)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ProcessEntity(
                    child,
                    insertLayerName,
                    insertDepth + 1,
                    context,
                    cancellationToken
                );
            }
        }

        private static bool ShouldProcessEntity(
            EntityObject entity,
            string layerName,
            DxfLoadOptions options)
        {
            if (options.IgnoreInvisibleEntities &&
                !entity.IsVisible)
            {
                return false;
            }

            if (options.IgnoreInvisibleLayers &&
                !entity.Layer.IsVisible)
            {
                return false;
            }

            if (options.IgnoreFrozenLayers &&
                entity.Layer.IsFrozen)
            {
                return false;
            }

            if (options.IgnoreNonPlotLayers &&
                !entity.Layer.Plot)
            {
                return false;
            }

            if (options.IncludedLayers.Count > 0 &&
                !options.IncludedLayers.Contains(layerName))
            {
                return false;
            }

            if (options.ExcludedLayers.Contains(layerName))
            {
                return false;
            }

            return true;
        }
        /// <summary>
        /// 현재 Entity가 사용할 실제 레이어 이름을 결정합니다.
        /// 일반 Entity는 자신의 레이어를 사용하며, Block 내부의 Layer 0 Entity는
        /// 해당 Block을 참조한 Insert의 레이어 이름을 상속합니다.
        /// </summary>
        /// <param name="entity">레이어를 확인할 현재 DXF Entity입니다.</param>
        /// <param name="inheritedLayerName">
        /// Block 내부 Entity가 Layer 0일 때 상속할 Insert의 레이어 이름입니다.
        /// </param>
        /// <returns>필터링과 Contour 생성에 사용할 최종 레이어 이름입니다.</returns>
        private static string ResolveLayerName(
            EntityObject entity,
            string? inheritedLayerName)
        {
            string entityLayer = entity.Layer?.Name ?? "0";

            // Block 내부의 Layer 0은 Insert가 놓인 Layer를 상속합니다.
            if (entityLayer.Equals(
                    "0",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(inheritedLayerName))
            {
                return inheritedLayerName;
            }

            return entityLayer;
        }
    }
}
