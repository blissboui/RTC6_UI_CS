
namespace RTC6_UI.Dxf
{
    public enum DxfMotionType
    {
        Jump,
        Mark
    }

    public readonly record struct DxfMotionCommand(
        DxfMotionType Type,
        double X,
        double Y,
        string LayerName
    );
}
