using System;
using RTC6Import;
using RTC6_UI.Settings;

namespace RTC6_UI.Services
{
    /// <summary>
    /// 저장된 시스템 설정 중 SCANahead 품질과 Delay 설정을 RTC6에 적용합니다.
    /// 이 클래스는 TEST 모드만 허용하며 실제 레이저 출력은 활성화하지 않습니다.
    /// </summary>
    public sealed class Rtc6SystemSettingsApplier
    {
        private readonly Rtc6Controller _rtc6Controller;

        /// <summary>
        /// 마지막 RTC6 설정 적용 오류입니다.
        /// </summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// 설정을 적용할 RTC6 Controller를 전달받습니다.
        /// </summary>
        public Rtc6SystemSettingsApplier(Rtc6Controller rtc6Controller)
        {
            _rtc6Controller = rtc6Controller ?? throw new ArgumentNullException(nameof(rtc6Controller));
        }

        /// <summary>
        /// TEST 모드에서 SCANahead 품질과 자동 또는 수동 Delay 설정을 즉시 적용합니다.
        /// 실제 레이저 모드, 주파수, 펄스폭 및 레이저 활성화는 수행하지 않습니다.
        /// </summary>
        public bool ApplyForTest(SystemSettings settings)
        {
            LastError = string.Empty;

            if (!_rtc6Controller.IsInitialized)
            {
                LastError = "RTC6 초기화를 먼저 실행하세요.";
                return false;
            }

            if (settings.OperationMode != OperationMode.Test)
            {
                LastError = "이 함수는 TEST 모드 설정만 적용할 수 있습니다.";
                return false;
            }

            if (!SystemSettingsValidator.Validate(settings, out string validationError))
            {
                LastError = validationError;
                return false;
            }

            if (_rtc6Controller.IsSimulationMode)
                return true;

            try
            {
                // 설정 적용 중 실제 레이저 출력이 발생하지 않도록 비활성화합니다.
                RTC6Wrap.disable_laser();

                // SCANahead의 코너, 선 끝 및 가감속 품질 스케일을 적용합니다.
                RTC6Wrap.set_scanahead_line_params(settings.CornerScalePercent, settings.EndScalePercent, settings.AccScalePercent);

                // 자동 Delay 결과에 추가할 레이저 ON/OFF 시점 보정값을 적용합니다.
                RTC6Wrap.set_scanahead_laser_shifts(settings.LaserShiftOn64, settings.LaserShiftOff64);

                if (settings.UseAutoDelay)
                {
                    // SCANahead가 레이저와 스캐너 Delay를 자동 계산하도록 설정합니다.
                    _ = RTC6Wrap.activate_scanahead_autodelays(1);
                }
                else
                {
                    // 자동 Delay를 해제한 뒤 사용자가 입력한 수동값을 적용합니다.
                    _ = RTC6Wrap.activate_scanahead_autodelays(0);

                    RTC6Wrap.set_laser_delays_ctrl(settings.LaserOnDelay64, settings.LaserOffDelay64);
                    RTC6Wrap.set_scanner_delays_ctrl(
                        ConvertScannerDelay(settings.ScannerJumpDelayMicroseconds),
                        ConvertScannerDelay(settings.ScannerMarkDelayMicroseconds),
                        ConvertScannerDelay(settings.ScannerPolygonDelayMicroseconds));
                }

                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    RTC6Wrap.disable_laser();
                }
                catch
                {
                    // 오류 처리 중 레이저 비활성화 실패는 원래 오류를 유지하기 위해 무시합니다.
                }

                LastError = BuildExceptionMessage("RTC6 시스템 설정 적용 중 오류가 발생했습니다.", exception);
                return false;
            }
        }

        /// <summary>
        /// UI의 µs 단위 Scanner Delay를 RTC6의 10µs 단위 값으로 변환합니다.
        /// </summary>
        private static uint ConvertScannerDelay(uint microseconds)
        {
            return checked((uint)Math.Round(microseconds / 10.0, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// 가장 안쪽에서 발생한 예외 내용을 사용자 메시지로 만듭니다.
        /// </summary>
        private static string BuildExceptionMessage(string title, Exception exception)
        {
            Exception root = exception;

            while (root.InnerException is not null)
                root = root.InnerException;

            return $"{title}\n종류: {root.GetType().Name}\n내용: {root.Message}";
        }
    }
}