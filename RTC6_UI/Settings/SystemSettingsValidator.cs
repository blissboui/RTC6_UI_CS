using System;
using System.Collections.Generic;
using RTC6_UI.Settings;

namespace RTC6_UI.Services
{
    /// <summary>
    /// 설정창에서 입력한 시스템 설정값이 사용 가능한 범위인지 검사합니다.
    /// 잘못된 설정을 JSON에 저장하거나 RTC6에 전달하는 것을 방지합니다.
    /// </summary>
    public static class SystemSettingsValidator
    {
        /// <summary>
        /// 전체 시스템 설정을 검사하고 오류 메시지를 반환합니다.
        /// </summary>
        public static bool Validate(SystemSettings settings, out string error)
        {
            ArgumentNullException.ThrowIfNull(settings);

            List<string> errors = new();

            if (settings.BoardNumber == 0)
                errors.Add("RTC6 보드 번호는 1 이상이어야 합니다.");

            if (string.IsNullOrWhiteSpace(settings.Rtc6FilesFolder))
                errors.Add("RTC6 프로그램 폴더가 비어 있습니다.");

            if (string.IsNullOrWhiteSpace(settings.CorrectionFileName))
                errors.Add("보정 파일명이 비어 있습니다.");

            if (!IsPositiveFinite(settings.BitsPerMillimeter))
                errors.Add("Bits/mm는 0보다 큰 정상적인 숫자여야 합니다.");

            if (!IsPositiveFinite(settings.FieldSizeMillimeter))
                errors.Add("필드 크기는 0보다 큰 정상적인 숫자여야 합니다.");

            if (!IsPositiveFinite(settings.EncoderPulsesPerMillimeter))
                errors.Add("엔코더 pulses/mm는 0보다 커야 합니다.");

            ValidatePercent(settings.CornerScalePercent, "CornerScale", errors);
            ValidatePercent(settings.EndScalePercent, "EndScale", errors);
            ValidatePercent(settings.AccScalePercent, "AccScale", errors);

            if (settings.DonePortMask == 0)
                errors.Add("DONE 포트 마스크는 0이 아니어야 합니다.");

            if (settings.EncoderTimeoutMilliseconds <= 0)
                errors.Add("엔코더 타임아웃은 0보다 커야 합니다.");

            if (!IsPositiveFinite(settings.LaserFrequencyKilohertz))
                errors.Add("레이저 주파수는 0보다 커야 합니다.");

            if (!IsPositiveFinite(settings.LaserPulseWidthMicroseconds))
                errors.Add("레이저 펄스폭은 0보다 커야 합니다.");

            if (!IsFinite(settings.FeedDirectionSkewPercent))
                errors.Add("기울어짐 보정값이 잘못되었습니다.");

            if (!IsFinite(settings.FlyScaleCorrectionPercent) || settings.FlyScaleCorrectionPercent <= -100.0)
                errors.Add("Fly 스케일 보정값은 -100%보다 커야 합니다.");

            error = string.Join(Environment.NewLine, errors);
            return errors.Count == 0;
        }

        /// <summary>
        /// SCANahead 스케일 값이 1~100% 범위인지 확인합니다.
        /// </summary>
        private static void ValidatePercent(uint value, string name, ICollection<string> errors)
        {
            if (value < 1 || value > 100)
                errors.Add($"{name}은 1~100% 범위여야 합니다.");
        }

        /// <summary>
        /// 값이 NaN이나 무한대가 아닌 양수인지 확인합니다.
        /// </summary>
        private static bool IsPositiveFinite(double value)
        {
            return IsFinite(value) && value > 0.0;
        }

        /// <summary>
        /// 값이 NaN이나 무한대가 아닌지 확인합니다.
        /// </summary>
        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}