using RTC6_UI.Dxf;
using RTC6_UI.Settings;

namespace RTC6_UI.Rtc6.Conversion
{
    /// <summary>
    /// 중심 이동이 완료된 DXF mm 좌표에 도안 X/Y 배율과 패턴 X/Y 오프셋을 적용합니다.
    /// 원본 명령 목록은 변경하지 않고 새로운 DXF 명령 목록을 반환합니다.
    /// </summary>
    public sealed class Rtc6PatternTransformer
    {
        /// <summary>
        /// DXF 명령 목록의 각 좌표에 X/Y 배율과 패턴 오프셋을 적용합니다.
        /// 배율을 먼저 적용한 후 오프셋을 더합니다.
        /// </summary>
        /// <param name="sourceCommands">중심 이동이 완료된 mm 단위 DXF 명령 목록입니다.</param>
        /// <param name="scaleX">X축 도안 배율입니다. 1.0이면 원래 크기입니다.</param>
        /// <param name="scaleY">Y축 도안 배율입니다. 1.0이면 원래 크기입니다.</param>
        /// <param name="offsetXMillimeter">배율 적용 후 X축으로 이동할 거리입니다.</param>
        /// <param name="offsetYMillimeter">배율 적용 후 Y축으로 이동할 거리입니다.</param>
        /// <returns>배율과 패턴 오프셋이 적용된 새로운 DXF 명령 목록입니다.</returns>
        public List<DxfMotionCommand> Transform(IReadOnlyList<DxfMotionCommand> sourceCommands, ModelSettings modelSettings)
        {
            ArgumentNullException.ThrowIfNull(sourceCommands);

            if (sourceCommands.Count == 0) throw new ArgumentException("변환할 DXF 이동 명령이 없습니다.", nameof(sourceCommands));
            
            double scaleX = modelSettings.PatternScaleX;
            double scaleY = modelSettings.PatternScaleY;
            double offsetXMillimeter = modelSettings.PatternOffsetXMillimeter;
            double offsetYMillimeter = modelSettings.PatternOffsetYMillimeter;

            ValidateScale(scaleX, nameof(scaleX));
            ValidateScale(scaleY, nameof(scaleY));
            ValidateOffset(offsetXMillimeter, nameof(offsetXMillimeter));
            ValidateOffset(offsetYMillimeter, nameof(offsetYMillimeter));

            List<DxfMotionCommand> transformedCommands = new(sourceCommands.Count);

            foreach (DxfMotionCommand command in sourceCommands)
            {
                ValidateCoordinate(command.X, command.Y);

                double transformedX = command.X * scaleX + offsetXMillimeter;
                double transformedY = command.Y * scaleY + offsetYMillimeter;

                transformedCommands.Add(new DxfMotionCommand(command.Type, transformedX, transformedY, command.LayerName));
            }

            return transformedCommands;
        }

        /// <summary>
        /// 도안 배율이 0보다 큰 정상적인 숫자인지 검사합니다.
        /// </summary>
        private static void ValidateScale(double scale, string parameterName)
        {
            if (!double.IsFinite(scale) || scale <= 0.0) 
                throw new ArgumentOutOfRangeException(parameterName, "도안 배율은 0보다 큰 정상적인 숫자여야 합니다.");
        }

        /// <summary>
        /// 패턴 오프셋이 정상적인 숫자인지 검사합니다.
        /// </summary>
        private static void ValidateOffset(double offsetMillimeter, string parameterName)
        {
            if (!double.IsFinite(offsetMillimeter)) 
                throw new ArgumentOutOfRangeException(parameterName, "패턴 오프셋은 정상적인 숫자여야 합니다.");
        }

        /// <summary>
        /// DXF 좌표가 NaN 또는 무한대가 아닌지 검사합니다.
        /// </summary>
        private static void ValidateCoordinate(double xMillimeter, double yMillimeter)
        {
            if (!double.IsFinite(xMillimeter) || !double.IsFinite(yMillimeter)) 
                throw new ArgumentOutOfRangeException(nameof(xMillimeter), $"정상적이지 않은 DXF 좌표가 있습니다. X={xMillimeter}, Y={yMillimeter}");
        }
    }
}