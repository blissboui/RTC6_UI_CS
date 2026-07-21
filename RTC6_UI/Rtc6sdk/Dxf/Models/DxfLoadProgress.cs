namespace RTC6_UI.Rtc6sdk.Dxf.Models
{
    /// <summary>
    /// DXF 로드 진행 상태를 화면에 전달하기 위한 데이터입니다.
    /// 현재 처리 번호, 전체 개수 및 처리 중인 Entity 종류를 저장합니다.
    /// </summary>
    public readonly record struct DxfLoadProgress(
            int Current,
            int Total,
            string EntityType
        );
}
