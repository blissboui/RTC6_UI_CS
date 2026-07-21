using System;
using System.Collections.Generic;
using System.Linq;
using RTC6_UI.Rtc6sdk.Dxf;
using RTC6_UI.Rtc6sdk.Dxf.Models;

namespace RTC6_UI.Rtc6sdk.Dxf.Internal
{
    /// <summary>
    /// 최종 Contour와 이동 명령을 기준으로 경계 및 통계 정보를 계산합니다.
    /// Contour 수, Point 수, Mark 거리, Jump 거리와 Entity 종류별 개수를 집계합니다.
    /// </summary>
    internal sealed class DxfResultCalculator
    {
        public DxfPathBounds? CalculateBounds(IReadOnlyList<DxfContour> contours)
        {
            if (contours.Count == 0)
            {
                return null;
            }

            double minimumX = double.MaxValue;
            double minimumY = double.MaxValue;
            double maximumX = double.MinValue;
            double maximumY = double.MinValue;

            foreach (DxfContour contour in contours)
            {
                foreach (DxfPathPoint point in contour.Points)
                {
                    minimumX = Math.Min(minimumX, point.X);
                    minimumY = Math.Min(minimumY, point.Y);
                    maximumX = Math.Max(maximumX, point.X);
                    maximumY = Math.Max(maximumY, point.Y);
                }
            }

            return new DxfPathBounds(
                minimumX,
                minimumY,
                maximumX,
                maximumY
            );
        }

        public void FillFinalStatistics(DxfLoadResult result)
        {
            result.Statistics.ContourCount = result.Contours.Count;

            result.Statistics.PointCount = result.Contours.Sum(contour => contour.Points.Count);    // (람다식) Contour 전체 포인트 개수

            result.Statistics.TotalMarkLengthMillimeter = result.Contours.Sum(contour => contour.MarkLength);   // 전체 mark 길이

            double jumpLength = 0.0;
            DxfPathPoint current = new(0.0, 0.0);

            foreach (DxfContour contour in result.Contours)
            {
                jumpLength += current.DistanceTo(contour.StartPoint);

                current = contour.EndPoint;
            }

            result.Statistics.EstimatedJumpLengthMillimeter = jumpLength;
        }

        public void CountEntity(Dictionary<string, int> dictionary, string key)
        {
            if (dictionary.TryGetValue(key, out int count))
            {
                dictionary[key] = count + 1;
            }
            else
            {
                dictionary[key] = 1;
            }
        }
    }
}
