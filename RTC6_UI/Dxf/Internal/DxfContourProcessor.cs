using System.Collections.Generic;
using RTC6_UI.Dxf.Models;

namespace RTC6_UI.Dxf.Internal
{
    internal sealed class DxfContourProcessor
    {
        public void AddContour(
            List<DxfPathPoint> points,
            bool isClosed,
            string layerName,
            string sourceType,
            string? sourceHandle,
            DxfExtractionContext context)
        {
            List<DxfPathPoint> cleaned =
                RemoveConsecutiveDuplicates(
                    points,
                    context.Options.WeldToleranceMillimeter
                );

            if (cleaned.Count > 1 &&
                cleaned[0].DistanceTo(cleaned[^1]) <=
                context.Options.WeldToleranceMillimeter)
            {
                cleaned.RemoveAt(cleaned.Count - 1);
                isClosed = true;
            }

            if (cleaned.Count < 2)
            {
                context.Result.Statistics.SkippedEntityCount++;
                return;
            }

            if (context.Options.RemoveZeroLengthContours)
            {
                bool hasLength = false;

                for (int index = 1;
                     index < cleaned.Count;
                     index++)
                {
                    if (cleaned[index - 1]
                            .DistanceTo(cleaned[index]) >
                        context.Options.WeldToleranceMillimeter)
                    {
                        hasLength = true;
                        break;
                    }
                }

                if (!hasLength)
                {
                    context.Result.Statistics.SkippedEntityCount++;
                    return;
                }
            }

            context.Contours.Add(
                new DxfContour
                {
                    LayerName = layerName,
                    SourceEntityType = sourceType,
                    SourceHandle = sourceHandle,
                    IsClosed = isClosed,
                    Points = cleaned
                }
            );

            context.TotalPointCount += cleaned.Count;
            context.Result.Statistics.SupportedEntityCount++;
        }

        public List<DxfContour> FinalizeContours(
            List<DxfContour> contours,
            DxfLoadOptions options)
        {
            // 기존 로직을 유지하기 위해 현재는 그대로 반환합니다.
            // 향후 중복 선 제거, 자가교차 검사 등을 이곳에 추가할 수 있습니다.
            _ = options;
            return contours;
        }

        public void AppendPoints(
            List<DxfPathPoint> destination,
            IReadOnlyList<DxfPathPoint> source,
            double tolerance)
        {
            if (source.Count == 0)
            {
                return;
            }

            int startIndex = 0;

            if (destination.Count > 0 &&
                destination[^1].DistanceTo(source[0]) <= tolerance)
            {
                startIndex = 1;
            }

            for (int index = startIndex;
                 index < source.Count;
                 index++)
            {
                destination.Add(source[index]);
            }
        }

        private static List<DxfPathPoint>
            RemoveConsecutiveDuplicates(
                IReadOnlyList<DxfPathPoint> points,
                double tolerance)
        {
            List<DxfPathPoint> result = new();

            foreach (DxfPathPoint point in points)
            {
                if (result.Count == 0 ||
                    result[^1].DistanceTo(point) > tolerance)
                {
                    result.Add(point);
                }
            }

            return result;
        }
    }
}
