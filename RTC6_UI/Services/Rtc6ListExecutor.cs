using RTC6Import;

namespace RTC6_UI.Services
{
    /// <summary>
    /// RTC6 get_status에서 반환되는 List 실행 상태와 출력 포인터 위치를 저장합니다.
    /// </summary>
    public readonly record struct Rtc6ListExecutionStatus(uint RawStatus, uint OutputPosition)
    {
        /// <summary>
        /// RTC6가 List를 처리하고 있거나 pause_list 또는 stop_list로 일시 정지된 상태인지 나타냅니다.
        /// </summary>
        public bool IsBusy => (RawStatus & 0x00000001) != 0;

        /// <summary>
        /// RTC6가 goto_xy 등의 시간이 필요한 Control 명령을 처리하고 있는지 나타냅니다.
        /// </summary>
        public bool IsInternalBusy => (RawStatus & 0x00000080) != 0;

        /// <summary>
        /// RTC6 List가 pause_list, stop_list 또는 set_wait에 의해 일시 정지된 상태인지 나타냅니다.
        /// </summary>
        public bool IsPaused => (RawStatus & 0x00008000) != 0;

        /// <summary>
        /// SCANahead 출력과 마지막 LaserOffDelay가 완전히 끝나지 않은 상태인지 나타냅니다.
        /// </summary>
        public bool IsHeadBusy => (RawStatus & 0x00800000) != 0;

        /// <summary>
        /// List 처리, 일시 정지, 내부 이동 또는 SCANahead 출력이 남아 있는지 나타냅니다.
        /// </summary>
        public bool IsActive => IsBusy || IsInternalBusy || IsPaused || IsHeadBusy;
    }

    /// <summary>
    /// Rtc6ListWriter가 작성한 RTC6 List를 시작하고 실행 완료 상태를 감시하며 실행 중인 List를 정지합니다.
    /// 레이저 활성화와 List 작성은 수행하지 않으며 execute_list, get_status, stop_execution만 담당합니다.
    /// </summary>
    public sealed class Rtc6ListExecutor
    {
        private const uint ResetAllRtc6Errors = uint.MaxValue;
        private const uint Rtc6BusyError = 0x00000020;

        private readonly Rtc6Controller _rtc6Controller;
        private readonly Rtc6ListWriter _rtc6ListWriter;
        private readonly object _rtc6Lock = new();

        /// <summary>
        /// 마지막으로 발생한 RTC6 List 실행 오류입니다.
        /// </summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// 마지막으로 실행을 요청한 RTC6 List 번호입니다.
        /// </summary>
        public uint LastExecutedListNumber { get; private set; }

        /// <summary>
        /// 마지막으로 확인한 RTC6 List 실행 상태입니다.
        /// </summary>
        public Rtc6ListExecutionStatus LastStatus { get; private set; }

        /// <summary>
        /// 현재 List 처리, 일시 정지, 내부 이동 또는 SCANahead 출력이 남아 있는지 나타냅니다.
        /// </summary>
        public bool IsExecuting { get; private set; }

        /// <summary>
        /// RTC6 초기화 상태와 마지막 List 작성 정보를 확인하기 위한 객체를 전달받습니다.
        /// </summary>
        public Rtc6ListExecutor(Rtc6Controller rtc6Controller, Rtc6ListWriter rtc6ListWriter)
        {
            _rtc6Controller = rtc6Controller ?? throw new ArgumentNullException(nameof(rtc6Controller));
            _rtc6ListWriter = rtc6ListWriter ?? throw new ArgumentNullException(nameof(rtc6ListWriter));
        }

        /// <summary>
        /// 지정된 RTC6 List가 현재 작성 완료된 List인지 확인한 후 execute_list 명령을 전송합니다.
        /// 이미 다른 List가 실행 중이거나 PAUSED 또는 INTERNAL-BUSY 상태이면 실행하지 않습니다.
        /// </summary>
        public bool Start(uint listNumber)
        {
            LastError = string.Empty;

            if (!ValidateBeforeStart(listNumber)) return false;

            if (_rtc6Controller.IsSimulationMode)
            {
                LastExecutedListNumber = listNumber;
                LastStatus = new Rtc6ListExecutionStatus(0, 0);
                IsExecuting = false;
                return true;
            }

            lock (_rtc6Lock)
            {
                try
                {
                    if (!TryReadStatusInternal(out Rtc6ListExecutionStatus status)) return false;
                    if (status.IsBusy) throw new InvalidOperationException("현재 RTC6 List가 실행 중이므로 새로운 List를 시작할 수 없습니다.");
                    if (status.IsPaused) throw new InvalidOperationException("현재 RTC6 List가 PAUSED 상태입니다. restart_list 또는 release_wait 처리가 필요합니다.");
                    if (status.IsInternalBusy) throw new InvalidOperationException("현재 RTC6가 내부 이동 명령을 처리 중이므로 완료 후 다시 시작하세요.");
                    if (status.IsHeadBusy) throw new InvalidOperationException("이전 SCANahead 출력 또는 마지막 LaserOffDelay가 끝나지 않았으므로 완료 후 다시 시작하세요.");

                    RTC6Wrap.reset_error(ResetAllRtc6Errors);
                    RTC6Wrap.execute_list(listNumber);

                    uint executeError = RTC6Wrap.get_last_error();
                    ThrowIfRtc6Error("RTC6 List 실행", executeError);

                    LastExecutedListNumber = listNumber;
                    IsExecuting = true;
                    return true;
                }
                catch (Exception exception)
                {
                    LastError = $"RTC6 List 실행 중 오류가 발생했습니다.\nList 번호: {listNumber}\n내용: {exception.Message}";
                    IsExecuting = false;
                    return false;
                }
            }
        }

        /// <summary>
        /// get_status를 주기적으로 확인하여 BUSY, INTERNAL-BUSY, PAUSED 및 HEAD BUSY 상태가 모두 해제될 때까지 비동기로 기다립니다.
        /// WPF UI 스레드를 점유하지 않으며 외부 CancellationToken으로 감시를 중단할 수 있습니다.
        /// </summary>
        public async Task<bool> WaitForCompletionAsync(int pollingIntervalMilliseconds = 20, CancellationToken cancellationToken = default)
        {
            LastError = string.Empty;

            if (pollingIntervalMilliseconds < 10 || pollingIntervalMilliseconds > 1000)
            {
                LastError = "RTC6 상태 확인 주기는 10ms 이상 1000ms 이하여야 합니다.";
                return false;
            }

            if (!_rtc6Controller.IsInitialized)
            {
                LastError = "RTC6 초기화를 먼저 실행하세요.";
                return false;
            }

            if (_rtc6Controller.IsSimulationMode)
            {
                IsExecuting = false;
                return true;
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryGetStatus(out Rtc6ListExecutionStatus status)) return false;
                if (!status.IsActive) return true;

                await Task.Delay(pollingIntervalMilliseconds, cancellationToken);
            }
        }

        /// <summary>
        /// RTC6 get_status를 호출하여 현재 실행 상태와 출력 포인터 위치를 읽습니다.
        /// </summary>
        public bool TryGetStatus(out Rtc6ListExecutionStatus status)
        {
            LastError = string.Empty;
            status = default;

            if (!_rtc6Controller.IsInitialized)
            {
                LastError = "RTC6 초기화를 먼저 실행하세요.";
                return false;
            }

            if (_rtc6Controller.IsSimulationMode)
            {
                LastStatus = new Rtc6ListExecutionStatus(0, 0);
                IsExecuting = false;
                status = LastStatus;
                return true;
            }

            lock (_rtc6Lock)
            {
                try
                {
                    return TryReadStatusInternal(out status);
                }
                catch (Exception exception)
                {
                    LastError = $"RTC6 List 상태 확인 중 오류가 발생했습니다.\n내용: {exception.Message}";
                    return false;
                }
            }
        }

        /// <summary>
        /// 현재 실행 중이거나 일시 정지된 RTC6 List에 stop_execution 명령을 전송합니다.
        /// 실행 중인 List가 없어서 RTC6_BUSY가 반환된 경우에는 이미 정지된 상태로 처리합니다.
        /// </summary>
        public bool Stop()
        {
            LastError = string.Empty;

            if (!_rtc6Controller.IsInitialized)
            {
                LastError = "RTC6가 초기화되지 않았습니다.";
                return false;
            }

            if (_rtc6Controller.IsSimulationMode)
            {
                IsExecuting = false;
                LastStatus = new Rtc6ListExecutionStatus(0, LastStatus.OutputPosition);
                return true;
            }

            lock (_rtc6Lock)
            {
                try
                {
                    RTC6Wrap.reset_error(ResetAllRtc6Errors);
                    RTC6Wrap.stop_execution();

                    uint stopError = RTC6Wrap.get_last_error();
                    if (stopError != 0 && stopError != Rtc6BusyError) ThrowIfRtc6Error("RTC6 List 정지", stopError);

                    IsExecuting = false;
                    return true;
                }
                catch (Exception exception)
                {
                    LastError = $"RTC6 List 정지 중 오류가 발생했습니다.\n내용: {exception.Message}";
                    return false;
                }
            }
        }

        /// <summary>
        /// RTC6 초기화 상태, List 번호 및 Rtc6ListWriter의 마지막 작성 정보를 검사합니다.
        /// </summary>
        private bool ValidateBeforeStart(uint listNumber)
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

            if (_rtc6ListWriter.LastWrittenListNumber != listNumber || _rtc6ListWriter.LastWrittenCommandCount <= 0)
            {
                LastError = $"RTC6 List {listNumber}이 현재 프로그램에서 작성 완료된 상태가 아닙니다. DXF 파일을 다시 불러와 List를 작성하세요.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// RTC6 get_status를 호출하고 읽은 상태를 객체 속성에 저장합니다.
        /// 호출자는 _rtc6Lock을 확보한 상태여야 합니다.
        /// </summary>
        private bool TryReadStatusInternal(out Rtc6ListExecutionStatus status)
        {
            status = default;

            RTC6Wrap.get_status(out uint rawStatus, out uint outputPosition);

            uint statusError = RTC6Wrap.get_last_error();
            if (statusError != 0)
            {
                LastError = $"RTC6 List 상태 확인 중 오류가 발생했습니다. 오류 코드: 0x{statusError:X8}";
                return false;
            }

            status = new Rtc6ListExecutionStatus(rawStatus, outputPosition);
            LastStatus = status;
            IsExecuting = status.IsActive;
            return true;
        }

        /// <summary>
        /// RTC6 오류 코드가 존재하면 작업 이름과 오류 코드를 포함한 예외를 발생시킵니다.
        /// </summary>
        private static void ThrowIfRtc6Error(string operationName, uint errorCode)
        {
            if (errorCode == 0) return;

            throw new InvalidOperationException($"{operationName} 중 RTC6 오류가 발생했습니다. 오류 코드: 0x{errorCode:X8}");
        }
    }
}
