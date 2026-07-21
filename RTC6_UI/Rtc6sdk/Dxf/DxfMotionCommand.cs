namespace RTC6_UI.Rtc6sdk.Dxf
{
    /// <summary>
    /// DXF 경로를 RTC6 이동 명령으로 변환할 때 사용하는 명령 종류입니다.
    /// Jump는 레이저를 사용하지 않는 이동이고, Mark는 가공 경로 이동입니다.
    /// </summary>
    public enum DxfMotionType
    {
        Jump,
        Mark
    }
    /// <summary>
    /// RTC6 List 작성 전에 사용하는 mm 단위 이동 명령입니다.
    /// Jump 또는 Mark 명령 종류와 목적지 X/Y 좌표, 레이어 이름을 저장합니다.
    /// </summary>
    public readonly record struct DxfMotionCommand(
        DxfMotionType Type,
        double X,
        double Y,
        string LayerName
    );
}
