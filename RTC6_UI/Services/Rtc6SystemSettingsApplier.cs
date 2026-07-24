using System;
using RTC6Import;
using RTC6_UI.Settings;
using System.Security.RightsManagement;

namespace RTC6_UI.Services
{
    /// <summary>
    /// SystemSettings에 저장된 RTC6 하드웨어 설정을 실제 RTC6 보드에 적용합니다.
    ///
    /// 즉시 적용되는 설정:
    /// - 레이저 모드와 펄스 파형
    /// - SCANahead 활성화 및 품질 파라미터
    /// - SCANahead Laser Shift
    /// - 자동 또는 수동 Delay
    ///
    /// List 작성 중 적용되는 설정:
    /// - set_fly_x 또는 set_fly_y OTF 보정
    ///
    /// 레이저 출력 활성화는 수행하지 않습니다.
    /// </summary>
    public sealed class Rtc6SystemSettingsApplier
    {
        private const uint ScanHeadNumber = 1;
        private const uint CorrectionTableNumber = 1;

        private const double MinimumFlyScale = 1.0 / 256.0;
        private const double MaximumFlyScale = 16000.0;

        private readonly Rtc6Controller _rtc6Controller;

        /// <summary>
        /// 마지막으로 발생한 설정 적용 오류입니다.
        /// </summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// 마지막으로 계산된 OTF Fly Scale입니다.
        /// 단위는 RTC6 bits/count입니다.
        /// </summary>
        public double LastFlyScale { get; private set; }

        /// <summary>
        /// 마지막으로 선택된 OTF 보정축입니다.
        /// </summary>
        public FeedAxis LastFlyAxis { get; private set; } = FeedAxis.X;

        /// <summary>
        /// 설정을 적용할 RTC6 Controller를 전달받습니다.
        /// </summary>
        public Rtc6SystemSettingsApplier(Rtc6Controller rtc6Controller)
        {
            _rtc6Controller = rtc6Controller ?? throw new ArgumentNullException(nameof(rtc6Controller));
        }

        /// <summary>
        /// RTC6 초기화가 완료된 후 SystemSettings의 즉시 적용 가능한 설정을 적용합니다.
        ///
        /// TEST 모드에서는 레이저 펄스를 0으로 설정합니다.
        /// Auto 모드에서는 주파수와 펄스폭을 설정하지만 레이저 출력은 활성화하지 않습니다.
        /// </summary>
        public bool Apply(SystemSettings settings)
        {
            LastError = string.Empty;

            if (!ValidateBeforeApply(settings))
                return false;

            if (_rtc6Controller.IsSimulationMode)
                return true;

            try
            {
                // 설정 변경 중 레이저 출력이 발생하지 않도록 먼저 비활성화합니다.
                RTC6Wrap.disable_laser();

                ApplyScanAheadSettings(settings);
                ApplyDelaySettings(settings);
                ApplyLaserSettings(settings);

                return true;
            }
            catch (Exception exception)
            {
                TryDisableLaser();
                LastError = BuildExceptionMessage("RTC6 시스템 설정 적용 중 오류가 발생했습니다.", exception);
                return false;
            }
        }

        /// <summary>
        /// 현재 작성 중인 RTC6 List에 OTF Fly 설정을 추가합니다.
        ///
        /// 반드시 set_start_list(), load_list() 호출 후 실제 Jump/Mark 명령을 추가하기 전에 호출해야 합니다.
        /// set_fly_x와 set_fly_y는 Control 명령이 아니라 List 명령입니다.
        /// </summary>
        public bool AppendFlySettingsToCurrentList(SystemSettings settings)
        {
            LastError = string.Empty;
            LastFlyScale = 0.0;

            if (!ValidateBeforeApply(settings))
                return false;

            if (settings.MotionCompensation == MotionCompensationMode.Disabled ||
                settings.FlyActivation == FlyActivationMode.Disabled)
            {
                return true;
            }

            if (settings.MotionCompensation != MotionCompensationMode.RtcFly ||
                settings.FlyActivation != FlyActivationMode.SetFlyAxis)
            {
                LastError = "현재 지원하지 않는 OTF 보정 방식입니다.";
                return false;
            }

            try
            {
                double flyScale = CalculateFlyScale(settings);
                FeedAxis flyAxis = ResolveFlyAxis(settings);

                ValidateFlyScale(flyScale);

                LastFlyScale = flyScale;
                LastFlyAxis = flyAxis;

                if (_rtc6Controller.IsSimulationMode)
                    return true;

                if (flyAxis == FeedAxis.X)
                    RTC6Wrap.set_fly_x(flyScale);
                else
                    RTC6Wrap.set_fly_y(flyScale);

                return true;
            }
            catch (Exception exception)
            {
                LastError = BuildExceptionMessage("RTC6 Fly 설정 적용 중 오류가 발생했습니다.", exception);
                return false;
            }
        }

        /// <summary>
        /// RTC6 초기화 상태와 SystemSettings 설정값을 검사합니다.
        /// </summary>
        private bool ValidateBeforeApply(SystemSettings settings)
        {
            if (settings is null)
            {
                LastError = "적용할 시스템 설정값이 없습니다.";
                return false;
            }

            if (!_rtc6Controller.IsInitialized)
            {
                LastError = "RTC6 초기화를 먼저 실행하세요.";
                return false;
            }

            if (!SystemSettingsValidator.Validate(settings, out string validationError))
            {
                LastError = validationError;
                return false;
            }

            return true;
        }

        /// <summary>
        /// SCANahead 기능을 활성화하고 품질 및 Laser Shift 설정을 적용합니다.
        /// </summary>
        private static void ApplyScanAheadSettings(SystemSettings settings)
        {
            uint result = RTC6Wrap.set_scanahead_params(
                1,
                ScanHeadNumber,
                CorrectionTableNumber,
                0,
                0,
                0.0);

            if (result != 0)
            {
                throw new InvalidOperationException(
                    $"SCANahead 활성화에 실패했습니다.\n" +
                    $"반환 코드: {result}\n" +
                    $"내용: {DescribeScanAheadError(result)}");
            }

            RTC6Wrap.set_scanahead_line_params(
                settings.CornerScalePercent,
                settings.EndScalePercent,
                settings.AccScalePercent);

            RTC6Wrap.set_scanahead_laser_shifts(
                settings.LaserShiftOn64,
                settings.LaserShiftOff64);
        }

        /// <summary>
        /// 자동 Delay 또는 사용자가 입력한 수동 Delay를 적용합니다.
        /// </summary>
        private static void ApplyDelaySettings(SystemSettings settings)
        {
            int requestedMode = settings.UseAutoDelay ? 1 : 0;
            int currentMode = RTC6Wrap.activate_scanahead_autodelays(requestedMode);

            if (currentMode != requestedMode)
            {
                throw new InvalidOperationException(
                    $"SCANahead Auto Delay 모드가 적용되지 않았습니다.\n" +
                    $"요청 모드: {requestedMode}\n" +
                    $"현재 모드: {currentMode}");
            }

            if (settings.UseAutoDelay)
                return;

            RTC6Wrap.set_laser_delays_ctrl(settings.LaserOnDelay64, settings.LaserOffDelay64);

            RTC6Wrap.set_scanner_delays_ctrl(
                ConvertScannerDelay(settings.ScannerJumpDelayMicroseconds),
                ConvertScannerDelay(settings.ScannerMarkDelayMicroseconds),
                ConvertScannerDelay(settings.ScannerPolygonDelayMicroseconds));
        }

        /// <summary>
        /// RTC6 레이저 모드와 펄스 파형을 설정합니다.
        ///
        /// TEST 모드에서는 펄스 출력을 비활성화합니다.
        /// Auto 모드에서도 레이저 활성화 명령은 호출하지 않습니다.
        /// </summary>
        private static void ApplyLaserSettings(SystemSettings settings)
        {
            RTC6Wrap.set_laser_mode((uint)settings.LaserMode);

            if (settings.OperationMode == OperationMode.Test)
            {
                RTC6Wrap.set_laser_pulses_ctrl(0, 0);
                return;
            }

            uint halfPeriod64 = ConvertFrequencyToHalfPeriod64(settings.LaserFrequencyKilohertz);
            uint pulseLength64 = ConvertMicrosecondsTo64(settings.LaserPulseWidthMicroseconds);

            if ((ulong)pulseLength64 >= (ulong)halfPeriod64 * 2)
            {
                throw new ArgumentOutOfRangeException(nameof(settings.LaserPulseWidthMicroseconds), "레이저 펄스폭은 출력 주기보다 작아야 합니다.");
            }

            RTC6Wrap.set_laser_pulses_ctrl(halfPeriod64, pulseLength64);
        }

        /// <summary>
        /// 레이저 주파수 kHz를 RTC6 HalfPeriod 값으로 변환합니다.
        /// RTC6 단위는 1/64µs입니다.
        /// </summary>
        private static uint ConvertFrequencyToHalfPeriod64(double frequencyKilohertz)
        {
            if (!double.IsFinite(frequencyKilohertz) || frequencyKilohertz <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(frequencyKilohertz), "레이저 주파수는 0보다 커야 합니다.");

            double halfPeriod64 = 32000.0 / frequencyKilohertz;

            if (halfPeriod64 < 1.0 || halfPeriod64 > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(frequencyKilohertz), "레이저 주파수를 RTC6 값으로 변환할 수 없습니다.");

            return checked((uint)Math.Round(halfPeriod64, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// µs 값을 RTC6의 1/64µs 단위로 변환합니다.
        /// </summary>
        private static uint ConvertMicrosecondsTo64(double microseconds)
        {
            if (!double.IsFinite(microseconds) || microseconds <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(microseconds), "레이저 펄스폭은 0보다 커야 합니다.");

            double value64 = microseconds * 64.0;

            if (value64 > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(microseconds), "레이저 펄스폭이 RTC6 범위를 벗어났습니다.");

            return checked((uint)Math.Round(value64, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// UI의 µs 단위 Scanner Delay를 RTC6의 10µs 단위로 변환합니다.
        /// </summary>
        private static uint ConvertScannerDelay(uint microseconds)
        {
            if (microseconds % 10 != 0)
                throw new ArgumentOutOfRangeException(nameof(microseconds), "Scanner Delay는 10µs 단위로 입력해야 합니다.");

            return microseconds / 10;
        }

        /// <summary>
        /// Bits/mm와 Encoder pulses/mm를 이용하여 RTC6 Fly Scale을 계산합니다.
        /// 계산 단위는 bits/count입니다.
        /// </summary>
        private static double CalculateFlyScale(SystemSettings settings)
        {
            if (!double.IsFinite(settings.BitsPerMillimeter) || settings.BitsPerMillimeter <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(settings.BitsPerMillimeter), "Bits/mm 값은 0보다 커야 합니다.");

            if (!double.IsFinite(settings.EncoderPulsesPerMillimeter) || settings.EncoderPulsesPerMillimeter <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(settings.EncoderPulsesPerMillimeter), "Encoder pulses/mm 값은 0보다 커야 합니다.");

            double scale = settings.BitsPerMillimeter / settings.EncoderPulsesPerMillimeter;
            scale *= 1.0 + settings.FlyScaleCorrectionPercent / 100.0;

            if (settings.EncoderPolarity == EncoderPolarity.Inverted)
                scale = -scale;

            if (settings.InvertFly)
                scale = -scale;

            return scale;
        }

        /// <summary>
        /// FeedAxis와 SwapFlyAxis 설정을 이용하여 실제 Fly 보정축을 결정합니다.
        /// </summary>
        private static FeedAxis ResolveFlyAxis(SystemSettings settings)
        {
            if (!settings.SwapFlyAxis)
                return settings.FeedAxis;

            return settings.FeedAxis == FeedAxis.X ? FeedAxis.Y : FeedAxis.X;
        }

        /// <summary>
        /// 계산된 Fly Scale이 RTC6 허용 범위 안에 있는지 확인합니다.
        /// </summary>
        private static void ValidateFlyScale(double flyScale)
        {
            if (!double.IsFinite(flyScale))
                throw new ArgumentOutOfRangeException(nameof(flyScale), "계산된 Fly Scale이 올바르지 않습니다.");

            double absoluteScale = Math.Abs(flyScale);

            if (absoluteScale < MinimumFlyScale || absoluteScale > MaximumFlyScale)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(flyScale),
                    $"Fly Scale 허용 범위는 절댓값 {MinimumFlyScale} 이상 {MaximumFlyScale} 이하입니다.\n" +
                    $"계산값: {flyScale}");
            }
        }

        /// <summary>
        /// set_scanahead_params 반환 코드의 의미를 반환합니다.
        /// </summary>
        private static string DescribeScanAheadError(uint errorCode)
        {
            return errorCode switch
            {
                1 => "RTC6 보드에 SCANahead 옵션이 활성화되어 있지 않습니다.",
                3 => "SCANahead 스캔 헤드를 찾지 못했거나 스캔 헤드 튜닝이 활성화되지 않았습니다.",
                5 => "현재 RTC6 List가 실행 중이어서 설정할 수 없습니다.",
                6 => "스캔 헤드 번호가 올바르지 않습니다.",
                7 => "계산된 스캔 헤드 스케일 값이 허용 범위를 벗어났습니다.",
                8 => "RTC6 보드가 응답하지 않거나 RTC6 프로그램이 로드되지 않았습니다.",
                11 => "RTC6 보드와 통신 중 PCI 전송 오류가 발생했습니다.",
                _ => "알 수 없는 SCANahead 오류입니다."
            };
        }

        /// <summary>
        /// 오류 처리 중 레이저 비활성화를 시도합니다.
        /// </summary>
        private static void TryDisableLaser()
        {
            try
            {
                RTC6Wrap.disable_laser();
            }
            catch
            {
                // 원래 발생한 오류를 유지하기 위해 비활성화 중 오류는 무시합니다.
            }
        }

        /// <summary>
        /// 가장 안쪽에서 발생한 실제 예외 내용을 사용자 메시지로 만듭니다.
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