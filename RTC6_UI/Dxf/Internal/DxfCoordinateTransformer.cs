using System;
using System.IO;
using netDxf;
using RTC6_UI.Dxf.Models;

namespace RTC6_UI.Dxf.Internal
{
    /// <summary>
    /// DXF 원본 3차원 좌표를 mm 단위의 2차원 좌표로 변환합니다.
    /// 단위, 배율, 축 반전, 회전, 오프셋 및 Z 평면 검사를 적용합니다.
    /// </summary>
    internal sealed class DxfCoordinateTransformer
    {
        public DxfPathPoint Transform(
            Vector3 source,
            DxfLoadOptions options,
            DxfLoadResult result)
        {
            if (!DxfValidator.IsFinite(source.X) ||
                !DxfValidator.IsFinite(source.Y) ||
                !DxfValidator.IsFinite(source.Z))
            {
                throw new InvalidDataException(
                    "DXF 좌표에 NaN 또는 무한대 값이 있습니다."
                );
            }

            double sourceZMillimeter = source.Z * options.SourceUnitToMillimeter;

            if (Math.Abs(sourceZMillimeter) >
                options.ZToleranceMillimeter)
            {
                string warning =
                    "Z=0 평면을 벗어난 Entity가 있습니다. " +
                    $"Z={sourceZMillimeter:F6} mm";

                if (options.RejectNonPlanarEntities)
                {
                    throw new InvalidDataException(warning);
                }

                if (!result.Warnings.Contains(warning))
                {
                    result.Warnings.Add(warning);
                }
            }

            double x =
                source.X *
                options.SourceUnitToMillimeter *
                options.Scale;

            double y =
                source.Y *
                options.SourceUnitToMillimeter *
                options.Scale;

            if (options.MirrorX)
            {
                x = -x;
            }

            if (options.MirrorY)
            {
                y = -y;
            }

            double radians =
                options.RotationDegrees *
                Math.PI / 180.0;

            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);

            double rotatedX = x * cosine - y * sine;
            double rotatedY = x * sine + y * cosine;

            return new DxfPathPoint(
                rotatedX + options.OffsetXMillimeter,
                rotatedY + options.OffsetYMillimeter
            );
        }
    }
}
