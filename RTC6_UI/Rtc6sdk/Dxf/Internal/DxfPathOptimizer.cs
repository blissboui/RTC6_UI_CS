using System.Collections.Generic;
using RTC6_UI.Rtc6sdk.Dxf.Models;

namespace RTC6_UI.Rtc6sdk.Dxf.Internal
{
    /// <summary>
    /// Contour 사이의 불필요한 Jump 이동 거리를 줄이도록 실행 순서를 정렬합니다.
    /// 필요하면 Contour 진행 방향을 반전하여 가까운 시작점을 선택합니다.
    /// </summary>
    internal sealed class DxfPathOptimizer
    {
        public List<DxfContour> OptimizeTravelOrder(IReadOnlyList<DxfContour> source, bool allowReverseOpenContours)    // 순서 최적화
        {
            List<DxfContour> remaining = new(source);

            List<DxfContour> optimized = new(source.Count);

            DxfPathPoint current = new(0.0, 0.0);

            while (remaining.Count > 0)
            {
                int bestIndex = 0;
                bool reverseBest = false;
                double bestDistance = double.MaxValue;

                for (int index = 0;
                     index < remaining.Count;
                     index++)
                {
                    DxfContour contour = remaining[index];

                    double startDistance =
                        current.DistanceTo(contour.StartPoint);

                    if (startDistance < bestDistance)
                    {
                        bestDistance = startDistance;
                        bestIndex = index;
                        reverseBest = false;
                    }

                    if (allowReverseOpenContours &&
                        !contour.IsClosed)
                    {
                        double endDistance =
                            current.DistanceTo(
                                contour.Points[^1]
                            );

                        if (endDistance < bestDistance)
                        {
                            bestDistance = endDistance;
                            bestIndex = index;
                            reverseBest = true;
                        }
                    }
                }

                DxfContour selected = remaining[bestIndex];

                remaining.RemoveAt(bestIndex);

                if (reverseBest)
                {
                    selected = selected.ReverseOpenContour();
                }

                optimized.Add(selected);
                current = selected.EndPoint;
            }

            return optimized;
        }
    }
}
