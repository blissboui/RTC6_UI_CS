using RTC6_UI.Settings;
using RTC6_UI.Rtc6.Models;

namespace RTC6_UI.Rtc6.Conversion
{
    /// <summary>
    /// mm 단위 경로 좌표를 RTC6 정수 좌표로 변환합니다.
    /// 축 반전과 스캔 필드 범위 검사도 함께 수행합니다.
    /// </summary>
    public sealed class Rtc6CoordinateConverter
    {
        private readonly SystemSettings _settings;

        /// <summary>
        /// 좌표 변환에 사용할 시스템 설정을 전달받습니다.
        /// </summary>
        public Rtc6CoordinateConverter(SystemSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// 중심이 0,0인 mm 좌표를 RTC6 정수 좌표로 변환합니다.
        /// </summary>
        public Rtc6Point Convert(double xMillimeter, double yMillimeter)
        {
            if (!IsInsideField(xMillimeter, yMillimeter))
                throw new ArgumentOutOfRangeException(nameof(xMillimeter), $"좌표가 스캔 필드를 벗어났습니다. X={xMillimeter:F4}, Y={yMillimeter:F4}");

            double x = _settings.FlipScanX ? -xMillimeter : xMillimeter;
            double y = _settings.FlipScanY ? -yMillimeter : yMillimeter;

            double rtcX = x * _settings.BitsPerMillimeter;
            double rtcY = y * _settings.BitsPerMillimeter;

            if (rtcX < int.MinValue || rtcX > int.MaxValue || rtcY < int.MinValue || rtcY > int.MaxValue)
                throw new OverflowException("변환된 RTC6 좌표가 Int32 범위를 벗어났습니다.");

            int convertedX = checked((int)Math.Round(rtcX, MidpointRounding.AwayFromZero));
            int convertedY = checked((int)Math.Round(rtcY, MidpointRounding.AwayFromZero));

            return new Rtc6Point(convertedX, convertedY);
        }

        /// <summary>
        /// 입력 좌표가 설정된 스캔 필드 범위 안에 있는지 확인합니다.
        /// </summary>
        public bool IsInsideField(double xMillimeter, double yMillimeter)
        {
            double halfField = _settings.FieldSizeMillimeter * 0.5;
            return Math.Abs(xMillimeter) <= halfField && Math.Abs(yMillimeter) <= halfField;
        }
    }
}