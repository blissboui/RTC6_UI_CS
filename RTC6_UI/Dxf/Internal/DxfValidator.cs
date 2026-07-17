using System;
using System.IO;

namespace RTC6_UI.Dxf.Internal
{
    internal static class DxfValidator
    {
        public static bool ValidateFilePath(
            string filePath,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                error = "DXF 파일 경로가 비어 있습니다.";
                return false;
            }

            if (!File.Exists(filePath))
            {
                error = "DXF 파일을 찾지 못했습니다.\n" + filePath;

                return false;
            }

            if (!Path.GetExtension(filePath).Equals(
                    ".dxf",
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "선택한 파일은 DXF 파일이 아닙니다.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool ValidateOptions(
            DxfLoadOptions options,
            out string error)
        {
            if (!IsFinite(options.SourceUnitToMillimeter) ||
                options.SourceUnitToMillimeter <= 0.0)
            {
                error = "SourceUnitToMillimeter는 0보다 커야 합니다.";

                return false;
            }

            if (!IsFinite(options.Scale) ||
                options.Scale <= 0.0)
            {
                error = "Scale은 0보다 커야 합니다.";
                return false;
            }

            if (options.CurvePrecision < 8)
            {
                error = "CurvePrecision은 8 이상이어야 합니다.";
                return false;
            }

            if (options.SplinePrecision < 2)
            {
                error = "SplinePrecision은 2 이상이어야 합니다.";
                return false;
            }

            if (options.WeldToleranceMillimeter < 0.0 ||
                !IsFinite(options.WeldToleranceMillimeter))
            {
                error = "WeldToleranceMillimeter 값이 잘못되었습니다.";

                return false;
            }

            if (options.ZToleranceMillimeter < 0.0 ||
                !IsFinite(options.ZToleranceMillimeter))
            {
                error = "ZToleranceMillimeter 값이 잘못되었습니다.";

                return false;
            }

            if (options.MaximumEntityCount <= 0 ||
                options.MaximumPointCount <= 0 ||
                options.MaximumInsertDepth <= 0)
            {
                error = "최대 개수 제한 값은 0보다 커야 합니다.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool IsFinite(double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }
    }
}
