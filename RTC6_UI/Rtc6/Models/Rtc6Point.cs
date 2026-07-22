namespace RTC6_UI.Rtc6.Models
{
    /// <summary>
    /// RTC6 좌표계에서 사용하는 하나의 X/Y 좌표점을 나타냅니다.
    /// mm 좌표에 BitsPerMillimeter를 적용한 정수 좌표를 저장합니다.
    /// </summary>
    /// <param name="X">RTC6 좌표계의 X축 정수 좌표입니다.</param>
    /// <param name="Y">RTC6 좌표계의 Y축 정수 좌표입니다.</param>
    public readonly record struct Rtc6Point(int X, int Y);
}