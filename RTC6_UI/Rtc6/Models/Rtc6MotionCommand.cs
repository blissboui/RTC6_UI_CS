namespace RTC6_UI.Rtc6.Models
{
    /// <summary>
    /// RTC6 List에 기록할 이동 명령의 종류입니다.
    /// </summary>
    public enum Rtc6MotionType
    {
        Jump,
        Mark
    }

    /// <summary>
    /// RTC6 List에 기록할 하나의 이동 명령입니다.
    /// </summary>
    /// <param name="Type">Jump 또는 Mark 명령 종류입니다.</param>
    /// <param name="X">RTC6 정수 X축 목표 좌표입니다.</param>
    /// <param name="Y">RTC6 정수 Y축 목표 좌표입니다.</param>
    public readonly record struct Rtc6MotionCommand(
        Rtc6MotionType Type,
        int X,
        int Y
    );
}