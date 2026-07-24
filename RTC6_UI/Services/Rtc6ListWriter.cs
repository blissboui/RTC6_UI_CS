using RTC6Import;
using RTC6_UI.Rtc6.Models;
using RTC6_UI.Settings;
using System;
using System.Linq;

namespace RTC6_UI.Services
{
    /// <summary>
    /// Rtc6CommandStore에 저장된 Jump 및 Mark 명령을 RTC6 보드의 지정된 List에 안전하게 기록합니다.
    /// 실행 중인 List가 덮어써지지 않도록 load_list를 사용하며, List 공간과 RTC6 누적 오류를 검사합니다.
    /// List 작성만 담당하며 작성된 List 실행과 레이저 활성화는 수행하지 않습니다.
    /// </summary>
    public sealed class Rtc6ListWriter
    {
        private const double MinimumRtc6SpeedBitsPerMillisecond = 1.6;
        private const double MaximumRtc6SpeedBitsPerMillisecond = 800000.0;
        private const uint ResetAllRtc6Errors = uint.MaxValue;

        private readonly Rtc6Controller _rtc6Controller;
        private readonly Rtc6SystemSettingsApplier _settingsApplier;
        private readonly object _rtc6Lock = new();

        /// <summary>
        /// 마지막으로 발생한 RTC6 List 작성 오류입니다.
        /// </summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// 마지막으로 작성한 RTC6 List 번호입니다.
        /// </summary>
        public uint LastWrittenListNumber { get; private set; }

        /// <summary>
        /// 마지막으로 RTC6 List에 작성한 Jump 및 Mark 이동 명령 개수입니다.
        /// </summary>
        public int LastWrittenCommandCount { get; private set; }

        /// <summary>
        /// RTC6 List 작성에 필요한 전체 List 명령 위치 개수입니다.
        /// 속도 설정, Fly 설정, 이동 명령 및 List 종료 명령을 포함합니다.
        /// </summary>
        public uint LastRequiredListPositionCount { get; private set; }

        /// <summary>
        /// RTC6 초기화 상태와 OTF 설정을 사용하기 위한 제어 객체를 전달받습니다.
        /// </summary>
        public Rtc6ListWriter(Rtc6Controller rtc6Controller, Rtc6SystemSettingsApplier settingsApplier)
        {
            _rtc6Controller = rtc6Controller ?? throw new ArgumentNullException(nameof(rtc6Controller));
            _settingsApplier = settingsApplier ?? throw new ArgumentNullException(nameof(settingsApplier));
        }

        /// <summary>
        /// Rtc6CommandStore의 명령을 지정된 RTC6 List에 기록합니다.
        /// 실행 중인 List 보호, List 공간 확인, 속도 변환 및 RTC6 누적 오류 검사를 수행합니다.
        /// </summary>
        public bool WriteList(uint listNumber, Rtc6CommandStore commandStore, SystemSettings systemSettings, ModelSettings modelSettings)
        {
            ResetResult();

            if (!ValidateBeforeWrite(listNumber, commandStore, systemSettings, modelSettings)) 
                return false;

            Rtc6MotionCommand[] commands = commandStore.Commands.ToArray();

            if (!ValidateCommands(commands)) return false;
            if (!TryConvertSpeed(modelSettings.JumpSpeedMillimeterPerSecond, systemSettings.BitsPerMillimeter, "Jump", out double jumpSpeed)) return false;
            if (!TryConvertSpeed(modelSettings.MarkingSpeedMillimeterPerSecond, systemSettings.BitsPerMillimeter, "Mark", out double markSpeed)) return false;
            if (!TryCalculateRequiredListPositions(commands.Length, systemSettings, out uint requiredListPositions)) return false;

            LastRequiredListPositionCount = requiredListPositions;

            if (_rtc6Controller.IsSimulationMode)
            {
                LastWrittenListNumber = listNumber;
                LastWrittenCommandCount = commands.Length;
                return true;
            }

            lock (_rtc6Lock)
            {
                bool listOpened = false;

                try
                {
                    RTC6Wrap.reset_error(ResetAllRtc6Errors);

                    uint openedListNumber = RTC6Wrap.load_list(listNumber, 0);
                    uint loadListError = RTC6Wrap.get_last_error();

                    ThrowIfRtc6Error("RTC6 List 열기", loadListError);

                    if (openedListNumber != listNumber) 
                        throw new InvalidOperationException($"RTC6 List {listNumber}을 열 수 없습니다. 해당 List가 실행 중이거나 사용할 수 없는 상태일 수 있습니다.");

                    listOpened = true;

                    uint availableListPositions = RTC6Wrap.get_list_space();
                    uint getListSpaceError = RTC6Wrap.get_last_error();

                    ThrowIfRtc6Error("RTC6 List 남은 공간 확인", getListSpaceError);

                    if (availableListPositions < requiredListPositions) 
                        throw new InvalidOperationException($"RTC6 List 공간이 부족합니다.\n필요 공간: {requiredListPositions}\n사용 가능 공간: {availableListPositions}");

                    RTC6Wrap.set_jump_speed(jumpSpeed);
                    RTC6Wrap.set_mark_speed(markSpeed);

                    if (!_settingsApplier.AppendFlySettingsToCurrentList(systemSettings)) 
                        throw new InvalidOperationException(_settingsApplier.LastError);

                    foreach (Rtc6MotionCommand command in commands) 
                        WriteMotionCommand(command);    // Jump/Mark 명령 추가

                    uint commandWriteError = RTC6Wrap.get_error();

                    ThrowIfRtc6Error("RTC6 List 명령 작성", commandWriteError);

                    RTC6Wrap.set_end_of_list();

                    uint listEndError = RTC6Wrap.get_error();

                    ThrowIfRtc6Error("RTC6 List 종료", listEndError);

                    listOpened = false;
                    LastWrittenListNumber = openedListNumber;
                    LastWrittenCommandCount = commands.Length;

                    return true;
                }
                catch (Exception exception)
                {
                    if (listOpened) TryReplaceWithEmptyList(listNumber);

                    LastError = $"RTC6 List 작성 중 오류가 발생했습니다.\nList 번호: {listNumber}\n내용: {exception.Message}";
                    return false;
                }
            }
        }

        /// <summary>
        /// 이전 RTC6 List 작성 결과와 오류 정보를 초기화합니다.
        /// </summary>
        private void ResetResult()
        {
            LastError = string.Empty;
            LastWrittenListNumber = 0;
            LastWrittenCommandCount = 0;
            LastRequiredListPositionCount = 0;
        }

        /// <summary>
        /// RTC6 초기화 상태, List 번호, 명령 저장 여부 및 설정값을 검사합니다.
        /// </summary>
        private bool ValidateBeforeWrite(uint listNumber, Rtc6CommandStore commandStore, SystemSettings systemSettings, ModelSettings modelSettings)
        {
            if (!_rtc6Controller.IsInitialized)
            {
                LastError = "RTC6 초기화를 먼저 실행하세요.";
                return false;
            }

            if (listNumber != 1 && listNumber != 2)
            {
                LastError = "RTC6 List 번호는 1 또는 2만 사용할 수 있습니다.";
                return false;
            }

            if (commandStore is null)
            {
                LastError = "RTC6 명령 저장 객체가 없습니다.";
                return false;
            }

            if (!commandStore.HasCommands)
            {
                LastError = "RTC6 List에 작성할 이동 명령이 없습니다.";
                return false;
            }

            if (systemSettings is null)
            {
                LastError = "시스템 설정값이 없습니다.";
                return false;
            }

            if (modelSettings is null)
            {
                LastError = "모델 설정값이 없습니다.";
                return false;
            }

            if (!double.IsFinite(systemSettings.BitsPerMillimeter) || systemSettings.BitsPerMillimeter <= 0.0)
            {
                LastError = "Bits/mm 값은 0보다 큰 정상적인 숫자여야 합니다.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 명령 목록의 첫 명령이 Jump인지 확인하고 지원하지 않는 명령 종류가 있는지 검사합니다.
        /// 첫 명령을 Jump로 제한하여 현재 스캐너 위치에서 의도하지 않은 Mark가 시작되는 것을 방지합니다.
        /// </summary>
        private bool ValidateCommands(Rtc6MotionCommand[] commands)
        {
            if (commands.Length == 0)
            {
                LastError = "RTC6 List에 작성할 명령이 없습니다.";
                return false;
            }

            if (commands[0].Type != Rtc6MotionType.Jump)
            {
                LastError = "RTC6 List의 첫 번째 이동 명령은 반드시 Jump 명령이어야 합니다.";
                return false;
            }

            for (int index = 0; index < commands.Length; index++)
            {
                if (commands[index].Type == Rtc6MotionType.Jump || commands[index].Type == Rtc6MotionType.Mark) continue;

                LastError = $"지원하지 않는 RTC6 명령이 있습니다. 명령 순번: {index + 1}, 명령 종류: {commands[index].Type}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// mm/s 단위 속도를 RTC6 속도 단위인 bits/ms로 변환하고 RTC6 허용 범위를 검사합니다.
        /// </summary>
        private bool TryConvertSpeed(double speedMillimeterPerSecond, double bitsPerMillimeter, string speedName, out double convertedSpeed)
        {
            convertedSpeed = 0.0;

            if (!double.IsFinite(speedMillimeterPerSecond) || speedMillimeterPerSecond <= 0.0)
            {
                LastError = $"{speedName} 속도는 0보다 큰 정상적인 숫자여야 합니다.";
                return false;
            }

            convertedSpeed = speedMillimeterPerSecond * bitsPerMillimeter / 1000.0;

            if (!double.IsFinite(convertedSpeed))
            {
                LastError = $"{speedName} 속도를 RTC6 속도로 변환할 수 없습니다.";
                return false;
            }

            // 속도 최소/최대 범위 검사
            if (convertedSpeed < MinimumRtc6SpeedBitsPerMillisecond || convertedSpeed > MaximumRtc6SpeedBitsPerMillisecond)
            {
                LastError = $"{speedName} RTC6 변환 속도가 허용 범위를 벗어났습니다.\n변환 속도: {convertedSpeed:F3} bits/ms\n허용 범위: {MinimumRtc6SpeedBitsPerMillisecond:F1}~{MaximumRtc6SpeedBitsPerMillisecond:F1} bits/ms";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 속도 설정, 선택적 Fly 설정, 이동 명령 및 set_end_of_list를 포함한 전체 List 공간을 계산합니다.
        /// </summary>
        private bool TryCalculateRequiredListPositions(int motionCommandCount, SystemSettings systemSettings, out uint requiredListPositions)
        {
            requiredListPositions = 0;

            int flyCommandCount = systemSettings.MotionCompensation == MotionCompensationMode.RtcFly && systemSettings.FlyActivation == FlyActivationMode.SetFlyAxis ? 1 : 0;
            long requiredCount = motionCommandCount + 3L + flyCommandCount;

            if (requiredCount <= 0 || requiredCount > uint.MaxValue)
            {
                LastError = "RTC6 List에 필요한 명령 공간을 계산할 수 없습니다.";
                return false;
            }

            requiredListPositions = (uint)requiredCount;
            return true;
        }

        /// <summary>
        /// 하나의 이동 명령 종류에 따라 현재 작성 중인 RTC6 List에 Jump 또는 Mark 명령을 추가합니다.
        /// </summary>
        private static void WriteMotionCommand(Rtc6MotionCommand command)
        {
            if (command.Type == Rtc6MotionType.Jump)
            {
                RTC6Wrap.jump_abs(command.X, command.Y);
                return;
            }

            if (command.Type == Rtc6MotionType.Mark)
            {
                RTC6Wrap.mark_abs(command.X, command.Y);
                return;
            }

            throw new InvalidOperationException($"지원하지 않는 RTC6 이동 명령입니다. 명령 종류: {command.Type}");
        }

        /// <summary>
        /// RTC6 오류 코드가 존재하면 오류 코드를 포함한 예외를 발생시킵니다.
        /// </summary>
        private static void ThrowIfRtc6Error(string operationName, uint errorCode)
        {
            if (errorCode == 0) return;

            throw new InvalidOperationException($"{operationName} 중 RTC6 오류가 발생했습니다. 오류 코드: 0x{errorCode:X8}");
        }

        /// <summary>
        /// List 작성 실패 시 부분 명령이 실행되지 않도록 해당 List의 첫 위치에 set_end_of_list만 기록하여 빈 List로 교체합니다.
        /// load_list를 사용하므로 List가 실행 중이면 강제로 덮어쓰지 않습니다.
        /// </summary>
        private static void TryReplaceWithEmptyList(uint listNumber)
        {
            try
            {
                RTC6Wrap.reset_error(ResetAllRtc6Errors);

                uint openedListNumber = RTC6Wrap.load_list(listNumber, 0);

                if (openedListNumber != listNumber) return;

                RTC6Wrap.set_end_of_list();
            }
            catch
            {
            }
        }
    }
}