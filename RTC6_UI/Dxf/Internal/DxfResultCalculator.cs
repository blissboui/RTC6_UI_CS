using System;
using System.Collections.Generic;
using System.Linq;
using RTC6_UI.Dxf.Models;

namespace RTC6_UI.Dxf.Internal
{
    internal sealed class DxfResultCalculator
    {
        public DxfPathBounds? CalculateBounds(
            IReadOnlyList<DxfContour> contours)
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
            result.Statistics.ContourCount =
                result.Contours.Count;

            result.Statistics.PointCount =
                result.Contours.Sum(
                    contour => contour.Points.Count
                );

            result.Statistics.TotalMarkLengthMillimeter =
                result.Contours.Sum(
                    contour => contour.MarkLength
                );

            double jumpLength = 0.0;
            DxfPathPoint current = new(0.0, 0.0);

            foreach (DxfContour contour in result.Contours)
            {
                jumpLength +=
                    current.DistanceTo(contour.StartPoint);

                current = contour.EndPoint;
            }

            result.Statistics.EstimatedJumpLengthMillimeter =
                jumpLength;
        }

        public void CountEntity(
            Dictionary<string, int> dictionary,
            string key)
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
