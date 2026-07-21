using System;
using System.Collections.Generic;
using System.Linq;
using netDxf;
using netDxf.Entities;
using RTC6_UI.Rtc6sdk.Dxf.Models;

namespace RTC6_UI.Rtc6sdk.Dxf.Internal
{
    /// <summary>
    /// LINE, ARC, CIRCLE, ELLIPSE, POLYLINE, SPLINE 등의 DXF Entity를
    /// 연속된 DxfPathPoint 목록으로 변환합니다.
    /// </summary>
    internal sealed class DxfGeometryConverter
    {
        private readonly DxfCoordinateTransformer _coordinateTransformer;
        private readonly DxfContourProcessor _contourProcessor;

        public DxfGeometryConverter(
            DxfCoordinateTransformer coordinateTransformer,
            DxfContourProcessor contourProcessor)
        {
            _coordinateTransformer = coordinateTransformer;
            _contourProcessor = contourProcessor;
        }

        public void AddLineContour(
            Line line,
            string layerName,
            DxfExtractionContext context)
        {
            List<DxfPathPoint> points = new()
            {
                _coordinateTransformer.Transform(
                    line.StartPoint,
                    context.Options,
                    context.Result
                ),
                _coordinateTransformer.Transform(
                    line.EndPoint,
                    context.Options,
                    context.Result
                )
            };

            _contourProcessor.AddContour(
                points,
                isClosed: false,
                layerName,
                "Line",
                line.Handle,
                context
            );
        }

        public void AddArcContour(
            Arc arc,
            string layerName,
            DxfExtractionContext context)
        {
            Polyline2D polyline =
                arc.ToPolyline2D(
                    context.Options.CurvePrecision
                );

            AddExplodedPolylineContour(
                polyline,
                isClosed: false,
                layerName,
                "Arc",
                arc.Handle,
                context
            );
        }

        public void AddCircleContour(
            Circle circle,
            string layerName,
            DxfExtractionContext context)
        {
            Polyline2D polyline =
                circle.ToPolyline2D(
                    context.Options.CurvePrecision
                );

            AddExplodedPolylineContour(
                polyline,
                isClosed: true,
                layerName,
                "Circle",
                circle.Handle,
                context
            );
        }

        public void AddEllipseContour(
            Ellipse ellipse,
            string layerName,
            DxfExtractionContext context)
        {
            Polyline2D polyline =
                ellipse.ToPolyline2D(
                    context.Options.CurvePrecision
                );

            AddExplodedPolylineContour(
                polyline,
                ellipse.IsFullEllipse,
                layerName,
                "Ellipse",
                ellipse.Handle,
                context
            );
        }

        public void AddPolyline2DContour(
            Polyline2D polyline,
            string layerName,
            DxfExtractionContext context)
        {
            AddExplodedPolylineContour(
                polyline,
                polyline.IsClosed,
                layerName,
                "Polyline2D",
                polyline.Handle,
                context
            );
        }

        public void AddPolyline3DContour(
            Polyline3D polyline,
            string layerName,
            DxfExtractionContext context)
        {
            List<Vector3> sourcePoints =
                polyline.PolygonalVertexes(
                    context.Options.SplinePrecision
                );

            List<DxfPathPoint> points =
                sourcePoints
                    .Select(
                        point => _coordinateTransformer.Transform(
                            point,
                            context.Options,
                            context.Result
                        )
                    )
                    .ToList();

            _contourProcessor.AddContour(
                points,
                polyline.IsClosed,
                layerName,
                "Polyline3D",
                polyline.Handle,
                context
            );
        }

        public void AddSplineContour(
            Spline spline,
            string layerName,
            DxfExtractionContext context)
        {
            List<Vector3> sourcePoints =
                spline.PolygonalVertexes(
                    context.Options.SplinePrecision
                );

            List<DxfPathPoint> points =
                sourcePoints
                    .Select(
                        point => _coordinateTransformer.Transform(
                            point,
                            context.Options,
                            context.Result
                        )
                    )
                    .ToList();

            bool isClosed =
                spline.IsClosed ||
                spline.IsClosedPeriodic;

            _contourProcessor.AddContour(
                points,
                isClosed,
                layerName,
                "Spline",
                spline.Handle,
                context
            );
        }

        private void AddExplodedPolylineContour(
            Polyline2D polyline,
            bool isClosed,
            string layerName,
            string sourceType,
            string? sourceHandle,
            DxfExtractionContext context)
        {
            List<EntityObject> segments = polyline.Explode();

            List<DxfPathPoint> points = new();

            foreach (EntityObject segment in segments)
            {
                List<DxfPathPoint> segmentPoints =
                    GetSegmentPoints(segment, context);

                _contourProcessor.AppendPoints(
                    points,
                    segmentPoints,
                    context.Options.WeldToleranceMillimeter
                );
            }

            _contourProcessor.AddContour(
                points,
                isClosed,
                layerName,
                sourceType,
                sourceHandle,
                context
            );
        }

        private List<DxfPathPoint> GetSegmentPoints(
            EntityObject entity,
            DxfExtractionContext context)
        {
            switch (entity)
            {
                case Line line:
                    return new List<DxfPathPoint>
                    {
                        _coordinateTransformer.Transform(
                            line.StartPoint,
                            context.Options,
                            context.Result
                        ),
                        _coordinateTransformer.Transform(
                            line.EndPoint,
                            context.Options,
                            context.Result
                        )
                    };

                case Arc arc:
                    {
                        Polyline2D polyline =
                            arc.ToPolyline2D(
                                context.Options.CurvePrecision
                            );

                        List<DxfPathPoint> points = new();

                        foreach (
                            EntityObject lineSegment
                            in polyline.Explode())
                        {
                            _contourProcessor.AppendPoints(
                                points,
                                GetSegmentPoints(
                                    lineSegment,
                                    context
                                ),
                                context.Options
                                    .WeldToleranceMillimeter
                            );
                        }

                        return points;
                    }

                case Ellipse ellipse:
                    {
                        Polyline2D polyline =
                            ellipse.ToPolyline2D(
                                context.Options.CurvePrecision
                            );

                        List<DxfPathPoint> points = new();

                        foreach (
                            EntityObject lineSegment
                            in polyline.Explode())
                        {
                            _contourProcessor.AppendPoints(
                                points,
                                GetSegmentPoints(
                                    lineSegment,
                                    context
                                ),
                                context.Options
                                    .WeldToleranceMillimeter
                            );
                        }

                        return points;
                    }

                default:
                    throw new NotSupportedException(
                        "Polyline 내부에서 지원하지 않는 " +
                        $"Entity가 발견되었습니다: {entity.Type}"
                    );
            }
        }
    }

}
