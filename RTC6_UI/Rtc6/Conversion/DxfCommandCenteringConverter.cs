using RTC6_UI.Dxf;

namespace RTC6_UI.Rtc6.Conversion
{
    /// <summary>
    /// DXF에서 생성된 mm 단위 이동 명령 목록의 중심을 계산하고, 도면 중심이 (0,0)이 되도록 모든 좌표를 이동합니다.
    /// 원본 명령 목록은 변경하지 않고 중심 이동된 새로운 명령 목록을 반환합니다.
    /// </summary>
    public sealed class DxfCommandCenteringConverter
    {
        /// <summary>
        /// 마지막으로 계산한 DXF 도면의 X축 중심 좌표입니다.
        /// </summary>
        public double CenterXMillimeter { get; private set; }

        /// <summary>
        /// 마지막으로 계산한 DXF 도면의 Y축 중심 좌표입니다.
        /// </summary>
        public double CenterYMillimeter { get; private set; }

        /// <summary>
        /// mm 단위 DXF 명령 목록의 중심을 계산하고, 중심이 (0,0)이 되도록 모든 좌표를 이동합니다.
        /// </summary>
        /// <param name="sourceCommands">중심 이동할 원본 DXF Jump/Mark 명령 목록입니다.</param>
        /// <returns>도면 중심이 (0,0)으로 이동된 새로운 DXF 명령 목록입니다.</returns>
        public List<DxfMotionCommand> Convert(IReadOnlyList<DxfMotionCommand> sourceCommands)
        {
            ArgumentNullException.ThrowIfNull(sourceCommands);

            if (sourceCommands.Count == 0) 
                throw new ArgumentException("중심 이동할 DXF 명령이 없습니다.", nameof(sourceCommands));

            CalculateCenter(sourceCommands);    // 중심 계산

            List<DxfMotionCommand> centeredCommands = new(sourceCommands.Count);

            // 중심을 (0,0)으로 이동
            foreach (DxfMotionCommand command in sourceCommands)
            {
                double centeredX = command.X - CenterXMillimeter;
                double centeredY = command.Y - CenterYMillimeter;

                centeredCommands.Add(new DxfMotionCommand(command.Type, centeredX, centeredY, command.LayerName));
            }

            return centeredCommands;
        }

        /// <summary>
        /// 전체 DXF 명령 좌표의 최소값과 최대값을 이용해 도면 중심을 계산합니다.
        /// </summary>
        private void CalculateCenter(IReadOnlyList<DxfMotionCommand> commands)
        {
            double minimumX = double.MaxValue;
            double minimumY = double.MaxValue;
            double maximumX = double.MinValue;
            double maximumY = double.MinValue;

            foreach (DxfMotionCommand command in commands)
            {
                ValidateCoordinate(command.X, command.Y);

                minimumX = Math.Min(minimumX, command.X);
                minimumY = Math.Min(minimumY, command.Y);
                maximumX = Math.Max(maximumX, command.X);
                maximumY = Math.Max(maximumY, command.Y);
            }

            CenterXMillimeter = (minimumX + maximumX) * 0.5;
            CenterYMillimeter = (minimumY + maximumY) * 0.5;
        }

        /// <summary>
        /// DXF 좌표가 NaN 또는 무한대가 아닌 정상적인 숫자인지 검사합니다.
        /// </summary>
        private static void ValidateCoordinate(double xMillimeter, double yMillimeter)
        {
            if (!double.IsFinite(xMillimeter) || !double.IsFinite(yMillimeter)) throw new ArgumentOutOfRangeException(nameof(xMillimeter), $"정상적이지 않은 DXF 좌표가 있습니다. X={xMillimeter}, Y={yMillimeter}");
        }
    }
}