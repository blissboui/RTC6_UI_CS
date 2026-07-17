
namespace RTC6_UI.Dxf.Models
{
    public readonly record struct DxfLoadProgress(
            int Current,
            int Total,
            string EntityType
        );
}
