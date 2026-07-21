using System.Collections.Generic;
using System;
using RTC6_UI.Rtc6sdk.Dxf;
using RTC6_UI.Rtc6sdk.Dxf.Models;

namespace RTC6_UI.Rtc6sdk.Dxf.Internal
{
    /// <summary>
    /// 추출된 Contour를 최종 가공 가능한 형태로 정리합니다.
    /// 연속점 중복 제거, 짧은 경로 제거, 연결된 Contour 병합 및 닫힌 경로 판정을 수행합니다.
    /// </summary>
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

        public List<DxfContour> FinalizeContours( List<DxfContour> contours, DxfLoadOptions options)    // Dxf->mm 변환 최종 후처리
        {
            // 기존 로직을 유지하기 위해 현재는 그대로 반환합니다.
            // 향후 중복 선 제거, 자가교차 검사 등을 이곳에 추가할 수 있습니다.
            // 이 함수에 순서 정렬 알고리즘 추가

            // 유효하지 않은 Contour를 제거하고 연결된 Contour를 최종 병합
            MergeConnectedContours(contours, options.WeldToleranceMillimeter);
            return contours;
        }

        public void AppendPoints(
            List<DxfPathPoint> destination,
            IReadOnlyList<DxfPathPoint> source,
            double tolerance)   // 점 추가
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

        private static void MergeConnectedContours(List<DxfContour> contours, double tolerance) // 연결 가능한 Contour가 없을 때까지 전체 목록을 반복하며 병합
        {
            bool merged;

            do
            {
                merged = false;

                for (int firstIndex = 0; firstIndex < contours.Count - 1 && !merged; firstIndex++)
                {
                    DxfContour first = contours[firstIndex];
                    if (first.IsClosed) continue;

                    for (int secondIndex = firstIndex + 1; secondIndex < contours.Count; secondIndex++)
                    {
                        DxfContour second = contours[secondIndex];

                        if (second.IsClosed || !IsSameLayer(first, second)) continue;
                        if (!TryMerge(first, second, tolerance)) continue;

                        UpdateClosedState(first, tolerance);
                        contours.RemoveAt(secondIndex);
                        merged = true;
                        break;
                    }
                }
            }
            while (merged);
        }

        private static bool TryMerge(DxfContour first, DxfContour second, double tolerance) // 두 Contour의 시작점과 끝점을 비교하여 가능한 방향으로 병합
        {
            DxfPathPoint firstStart = first.Points[0];
            DxfPathPoint firstEnd = first.Points[^1];
            DxfPathPoint secondStart = second.Points[0];
            DxfPathPoint secondEnd = second.Points[^1];

            // A 끝점 → B 시작점
            if (AreSamePoint(firstEnd, secondStart, tolerance))
            {
                AppendForward(first.Points, second.Points, skipFirst: true);
                return true;
            }

            // A 끝점 → B 끝점
            if (AreSamePoint(firstEnd, secondEnd, tolerance))
            {
                AppendReverse(first.Points, second.Points, skipLast: true);
                return true;
            }

            // B 끝점 → A 시작점
            if (AreSamePoint(secondEnd, firstStart, tolerance))
            {
                PrependForward(first.Points, second.Points, skipLast: true);
                return true;
            }

            // B 시작점 → A 시작점
            if (AreSamePoint(secondStart, firstStart, tolerance))
            {
                PrependReverse(first.Points, second.Points, skipFirst: true);
                return true;
            }

            return false;
        }

        private static void AppendForward(List<DxfPathPoint> destination, IReadOnlyList<DxfPathPoint> source, bool skipFirst) // 두 번째 Contour를 정방향으로 첫 번째 Contour의 뒤에 추가
        {
            int startIndex = skipFirst ? 1 : 0;

            for (int index = startIndex; index < source.Count; index++)
                destination.Add(source[index]);
        }

        private static void AppendReverse(List<DxfPathPoint> destination, IReadOnlyList<DxfPathPoint> source, bool skipLast)  // 두 번째 Contour를 역방향으로 첫 번째 Contour의 뒤에 추가
        {
            int startIndex = source.Count - (skipLast ? 2 : 1);

            for (int index = startIndex; index >= 0; index--)
                destination.Add(source[index]);
        }

        private static void PrependForward(
            List<DxfPathPoint> destination,
            IReadOnlyList<DxfPathPoint> source,
            bool skipLast)  // 두 번째 Contour를 정방향으로 첫 번째 Contour의 앞에 추가
        {
            int count = source.Count - (skipLast ? 1 : 0);
            destination.InsertRange(0, GetForwardPoints(source, count));
        }

        private static void PrependReverse(
            List<DxfPathPoint> destination,
            IReadOnlyList<DxfPathPoint> source,
            bool skipFirst) // 두 번째 Contour를 역방향으로 첫 번째 Contour의 앞에 추가
        {
            int lastIndex = skipFirst ? 1 : 0;
            List<DxfPathPoint> points = new(source.Count - lastIndex);

            for (int index = source.Count - 1; index >= lastIndex; index--)
                points.Add(source[index]);

            destination.InsertRange(0, points);
        }

        private static List<DxfPathPoint> GetForwardPoints(
            IReadOnlyList<DxfPathPoint> source,
            int count)  // IReadOnlyList에서 앞쪽에 삽입할 정방향 점 목록 생성
        {
            List<DxfPathPoint> points = new(count);

            for (int index = 0; index < count; index++)
                points.Add(source[index]);

            return points;
        }

        private static void UpdateClosedState(DxfContour contour, double tolerance) // 병합된 Contour의 시작점과 끝점을 비교하여 닫힌 경로 여부 갱신
        {
            if (contour.Points.Count < 3) return;
            if (!AreSamePoint(contour.Points[0], contour.Points[^1], tolerance)) return;

            contour.Points.RemoveAt(contour.Points.Count - 1);
            contour.IsClosed = true;
        }

        private static bool IsSameLayer(DxfContour first, DxfContour second)    // 두 Contour가 동일한 레이어에 속하는지 대소문자 구분 없이 확인
        {
            return string.Equals(
                first.LayerName,
                second.LayerName,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool AreSamePoint(
            DxfPathPoint first,
            DxfPathPoint second,
            double tolerance)   // 두 점 사이의 거리가 허용 오차 이하인지 확인
        {
            return first.DistanceTo(second) <= tolerance;
        }
    }
}
