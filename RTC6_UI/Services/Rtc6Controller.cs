// ============================================================
// 파일: Rtc6Controller.cs
// 역할:
// 1. 실제 RTC6 초기화
// 2. 시뮬레이션 모드 지원
// 3. RTC6 프로그램 및 Correction 파일 로드
// 4. 스캐너 Jump 이동
// 5. 정지 및 DLL 해제
// ============================================================

using System;
using RTC6Import;

namespace RTC6_UI.Services
{
    public sealed class Rtc6Controller : IDisposable
    {
        // RTC6 제어가 가능한 상태인지 저장
        private bool _initialized;

        // RTC6 DLL 초기화가 실행됐는지 저장
        private bool _dllOpened;

        // RTC6 프로그램 파일 로드 상태
        private bool _programLoaded;

        // Correction 파일 로드 상태
        private bool _correctionLoaded;

        // 시뮬레이션 모드인지 저장
        private bool _simulationMode;

        // Dispose 중복 실행 방지
        private bool _disposed;

        // 마지막 오류 메시지
        public string LastError { get; private set; } = string.Empty;

        // 현재 상태를 MainWindow에서 확인하기 위한 속성
        public bool IsInitialized => _initialized;
        public bool IsProgramLoaded => _programLoaded;
        public bool IsCorrectionLoaded => _correctionLoaded;
        public bool IsSimulationMode => _simulationMode;

        // ====================================================
        // RTC6 초기화
        //
        // simulationMode가 true이면:
        // - RTC6 DLL을 호출하지 않음
        // - 보드가 없어도 초기화 성공
        //
        // simulationMode가 false이면:
        // - 실제 RTC6 보드 초기화
        // - 프로그램 및 Correction 파일 로드
        // ====================================================

        public bool Initialize(
            ushort boardNumber,
            string programFolderPath,
            string correctionFilePath,
            bool simulationMode)
        {
            LastError = string.Empty;

            // 기존 연결 또는 초기화 상태 정리
            Shutdown();

            _simulationMode = simulationMode;

            // ------------------------------------------------
            // 1. 시뮬레이션 모드
            // ------------------------------------------------

            if (_simulationMode)
            {
                // 실제 DLL과 보드를 사용하지 않습니다.
                _initialized = true;
                _programLoaded = true;
                _correctionLoaded = true;

                return true;
            }

            // ------------------------------------------------
            // 2. 최소한의 입력값 검사
            //
            // 폴더와 파일의 실제 존재 여부는 Initialize 호출 전에
            // Rtc6FileValidator에서 검사합니다.
            // ------------------------------------------------

            if (string.IsNullOrWhiteSpace(programFolderPath) ||
                string.IsNullOrWhiteSpace(correctionFilePath))
            {
                LastError = "RTC6 초기화에 필요한 파일 경로가 비어 있습니다.";
                return false;
            }

            try
            {
                // ------------------------------------------------
                // 3. RTC6 DLL 초기화
                // ------------------------------------------------

                uint initResult = RTC6Wrap.init_rtc6_dll();

                // DLL 초기화 함수를 호출했으므로
                // 종료할 때 free_rtc6_dll()을 실행하도록 표시합니다.
                _dllOpened = true;

                if (initResult != 0)
                {
                    LastError =
                        "RTC6 DLL 초기화에 실패했습니다.\n" +
                        $"반환 코드: {initResult}\n\n" +
                        "보드 연결과 RTC6 드라이버 설치 상태를 확인하세요.";

                    Shutdown();
                    return false;
                }

                // ------------------------------------------------
                // 4. 연결된 RTC6 보드 개수 확인
                // ------------------------------------------------

                uint boardCount = RTC6Wrap.rtc6_count_cards();

                if (boardCount == 0)
                {
                    LastError =
                        "연결된 RTC6 보드를 찾지 못했습니다.\n\n" +
                        "현재 보드가 연결되지 않았다면 " +
                        "시뮬레이션 모드를 사용하세요.";

                    Shutdown();
                    return false;
                }

                // ------------------------------------------------
                // 5. 사용할 보드 번호 검사
                // ------------------------------------------------

                if (boardNumber < 1 || boardNumber > boardCount)
                {
                    LastError =
                        "RTC6 보드 번호가 잘못되었습니다.\n" +
                        $"검색된 보드 수: {boardCount}\n" +
                        $"선택한 보드 번호: {boardNumber}";

                    Shutdown();
                    return false;
                }

                // ------------------------------------------------
                // 6. 사용할 RTC6 보드 선택
                // ------------------------------------------------

                RTC6Wrap.select_rtc(boardNumber);

                // ------------------------------------------------
                // 7. RTC6 시스템 프로그램 로드
                //
                // RTC6DAT.dat와 RTC6OUT.out 파일이 들어 있는
                // 폴더 경로를 전달합니다.
                // ------------------------------------------------

                uint programResult =
                    RTC6Wrap.load_program_file(programFolderPath);

                if (programResult != 0)
                {
                    LastError =
                        "RTC6 프로그램 로드에 실패했습니다.\n" +
                        $"반환 코드: {programResult}\n" +
                        $"프로그램 폴더: {programFolderPath}";

                    Shutdown();
                    return false;
                }

                _programLoaded = true;

                // ------------------------------------------------
                // 8. Correction 파일 로드
                //
                // 1: Correction Table 1
                // 2: X/Y 2차원 보정
                // ------------------------------------------------

                uint correctionResult =
                    RTC6Wrap.load_correction_file(
                        correctionFilePath,
                        1,
                        2);

                if (correctionResult != 0)
                {
                    LastError =
                        "Correction 파일 로드에 실패했습니다.\n" +
                        $"반환 코드: {correctionResult}\n" +
                        $"파일: {correctionFilePath}";

                    Shutdown();
                    return false;
                }

                // Scan Head A에서 Correction Table 1 사용
                RTC6Wrap.select_cor_table(1, 0);

                _correctionLoaded = true;
                _initialized = true;

                return true;
            }
            catch (Exception exception)
            {
                Exception rootException = exception;

                // 가장 안쪽의 실제 오류를 찾습니다.
                while (rootException.InnerException is not null)
                    rootException = rootException.InnerException;

                LastError =
                    "RTC6 초기화 중 예외가 발생했습니다.\n\n" +
                    $"오류 종류: {rootException.GetType().Name}\n" +
                    $"오류 내용: {rootException.Message}";

                Shutdown();
                return false;
            }
        }

        // ====================================================
        // 지정 좌표로 Jump 이동
        //
        // jump_abs()만 사용하므로 이 코드에는
        // 레이저 ON 또는 Mark 명령이 없습니다.
        // ====================================================

        public bool MoveTo(
            int x,
            int y,
            double speed)
        {
            LastError = string.Empty;

            if (!_initialized)
            {
                LastError = "RTC6 초기화를 먼저 실행하세요.";
                return false;
            }

            if (speed <= 0.0)
            {
                LastError = "이동 속도는 0보다 커야 합니다.";
                return false;
            }

            // 시뮬레이션에서는 실제 RTC6 API를 호출하지 않습니다.
            if (_simulationMode)
            {
                return true;
            }

            try
            {
                //// List 1 작성 시작
                //RTC6Wrap.set_start_list(1);

                //// Jump 이동 속도 설정
                //RTC6Wrap.set_jump_speed(speed);
                //RTC6Wrap.set_mark_speed(speed);


                //// 지정한 절대 좌표로 이동
                ////RTC6Wrap.jump_abs(x, y);
                //RTC6Wrap.mark_abs(x, y);

                //// List 작성 종료
                //RTC6Wrap.set_end_of_list();

                //// List 1 실행
                //RTC6Wrap.execute_list(1);


                RTC6Wrap.set_start_list(1);

                RTC6Wrap.set_jump_speed(speed);

                RTC6Wrap.jump_abs(-x, 0);
                RTC6Wrap.long_delay(50000);

                RTC6Wrap.jump_abs(x, 0);
                RTC6Wrap.long_delay(50000);
                RTC6Wrap.list_repeat();


                RTC6Wrap.set_end_of_list();
                RTC6Wrap.execute_list(1);
                return true;
            }
            catch (Exception exception)
            {
                LastError =
                    "RTC6 이동 명령 실행 중 오류가 발생했습니다.\n" +
                    exception.Message;

                return false;
            }
        }

        // ====================================================
        // 중앙 X=0, Y=0으로 이동
        // ====================================================

        public bool MoveCenter(double speed)
        {
            return MoveTo(0, 0, speed);
        }

        // ====================================================
        // RTC6 실행 정지
        // ====================================================

        public bool Stop()
        {
            LastError = string.Empty;

            if (!_initialized)
            {
                LastError = "RTC6가 초기화되지 않았습니다.";
                return false;
            }

            // 시뮬레이션 모드에서는 실제 정지 API를 호출하지 않음
            if (_simulationMode)
            {
                return true;
            }

            if (!_dllOpened)
            {
                LastError = "RTC6 DLL이 초기화되지 않았습니다.";
                return false;
            }

            try
            {
                RTC6Wrap.stop_execution();

                return true;
            }
            catch (Exception exception)
            {
                LastError =
                    "RTC6 정지 중 오류가 발생했습니다.\n" +
                    exception.Message;

                return false;
            }
        }

        // ====================================================
        // RTC6 종료 및 자원 해제
        // ====================================================

        public void Shutdown()
        {
            _initialized = false;
            _programLoaded = false;
            _correctionLoaded = false;

            // 시뮬레이션 모드에서는 DLL을 사용하지 않았으므로
            // 상태값만 해제합니다.
            if (_simulationMode)
            {
                _simulationMode = false;
                _dllOpened = false;
                return;
            }

            if (!_dllOpened)
            {
                return;
            }

            try
            {
                // 실행 중인 List 정지
                RTC6Wrap.stop_execution();
            }
            catch
            {
                // 종료 중 정지 실패는 무시하고 DLL 해제를 계속합니다.
            }

            try
            {
                // RTC6 DLL 사용 종료
                RTC6Wrap.free_rtc6_dll();
            }
            catch
            {
                // 종료 중 발생한 예외는 프로그램 종료를 위해 무시합니다.
            }
            finally
            {
                _dllOpened = false;
            }
        }

        // ====================================================
        // IDisposable 구현
        // ====================================================

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Shutdown();

            _disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}