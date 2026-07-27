using Microsoft.Win32;
using RTC6_UI.Dxf;
using RTC6_UI.Dxf.Models;
using RTC6_UI.Rtc6.Conversion;
using RTC6_UI.Rtc6.Models;
using RTC6_UI.Services;
using RTC6_UI.Settings;
using System.ComponentModel;
using System.Windows;

// ============================================================
// 파일: MainWindow.xaml.cs
// 역할:
// 1. 시스템 설정 파일 로드 및 설정창 관리
// 2. RTC6 초기화, 이동, 정지 및 연결 종료
// 3. DXF 파일 선택 및 비동기 로드
// 4. DXF 로드 진행률과 결과 표시
// 5. 프로그램 동작 로그 표시
// ============================================================

namespace RTC6_UI;

/// <summary>
/// 프로그램의 메인 화면을 관리합니다.
/// 시스템 설정, RTC6 제어, DXF 파일 로드 및 로그 출력을 담당합니다.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Rtc6Controller _rtc6 = new();

    private readonly DxfLoader _dxfLoader = new();
    private CancellationTokenSource? _dxfLoadCts;

    private SystemSettings _systemSettings = new();

    private ModelSettings _modelSettings = new();

    private readonly SystemSettingsService _systemSettingsService = new();


    private DxfLoadResult? _loadedDxf;

    private readonly Rtc6SystemSettingsApplier _rtc6SettingsApplier;

    private readonly Rtc6CommandStore _rtc6CommandStore = new();

    private readonly Rtc6ListWriter _rtc6ListWriter;

    private readonly Rtc6ListExecutor _rtc6ListExecutor;
    private CancellationTokenSource? _rtc6ExecutionCts;

    /// <summary>
    /// 메인 화면을 초기화하고 시스템 설정을 불러옵니다.
    /// 창이 종료될 때 RTC6 및 DXF 작업을 정리하도록 Closing 이벤트를 등록합니다.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        _rtc6SettingsApplier = new Rtc6SystemSettingsApplier(_rtc6);
        _rtc6ListWriter = new Rtc6ListWriter(_rtc6, _rtc6SettingsApplier);
        _rtc6ListExecutor = new Rtc6ListExecutor(_rtc6, _rtc6ListWriter);

        LoadSystemSettings();
        Closing += MainWindow_Closing;
    }

    /// <summary>
    /// 시스템 설정창을 열고 사용자가 확인한 설정값을 system.json에 저장합니다.
    /// 사용자가 취소하면 기존 설정값을 유지합니다.
    /// </summary>
    private void OpenSystemSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SystemSettingsWindow window = new(_systemSettings, _modelSettings)
        {
            Owner = this
        };

        if (window.ShowDialog() != true || window.ResultSettings is null || window.ResultModelSettings is null) 
            return;

        _systemSettings = window.ResultSettings;
        _modelSettings = window.ResultModelSettings;
        _rtc6ListWriter.InvalidateWrittenList();
        StartListButton.IsEnabled = false;

        if (!_systemSettingsService.Save(_systemSettings))
        {
            MessageBox.Show(_systemSettingsService.LastError);
            return;
        }

        MessageBox.Show("시스템 설정이 저장되었습니다.");
    }

    /// <summary>
    /// DXF 파일 선택창을 열고 선택한 파일을 비동기로 불러옵니다.
    /// 로드 진행률, 결과, 생성된 명령 및 경고를 화면과 로그에 표시합니다.
    /// </summary>
    private async void OpenDxfButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "DXF 파일 선택",
            Filter = "DXF 파일 (*.dxf)|*.dxf|모든 파일 (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
            return;

        // 기존 DXF 로드 작업이 실행 중이면 취소하고 자원을 해제합니다.
        _dxfLoadCts?.Cancel();
        _dxfLoadCts?.Dispose();
        _dxfLoadCts = new CancellationTokenSource();

        PrepareDxfLoad(dialog.FileName);

        DxfLoadOptions options = CreateDxfLoadOptions();
        Progress<DxfLoadProgress> progress = new(UpdateDxfProgress);

        try
        {
            DxfLoadResult result = await _dxfLoader.LoadAsync(
                dialog.FileName,
                options,
                progress,
                _dxfLoadCts.Token);

            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, "DXF 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"DXF 로드 실패: {result.ErrorMessage}");
                return;
            }

            _loadedDxf = result;

            // Dxf -> Rtc6정수좌표 변환
            if (!PrepareRtc6Commands())
            {
                DxfProgressText.Text = "RTC6 좌표 변환 실패";
                return;
            }

            if (!_rtc6ListWriter.WriteList(1, _rtc6CommandStore, _systemSettings, _modelSettings))
            {
                DxfProgressText.Text = "RTC6 List 작성 실패";
                MessageBox.Show(_rtc6ListWriter.LastError, "List 작성 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"RTC6 List 작성 실패: {_rtc6ListWriter}");
                return;
            }

            ShowDxfResult(result);
            ShowDxfCommands(result);

            DxfProgressBar.Value = 100;
            DxfProgressText.Text = "로드 완료";

            AddLog($"DXF 로드 완료: Contour {result.Contours.Count}개, Command {result.Commands.Count}개");
            AddLog($"RTC6 이동 명령 {_rtc6CommandStore.Count}개 변환 완료");
            AddLog($"RTC6 LIST 작성 완료");
            StartListButton.IsEnabled = true;

            foreach (string warning in result.Warnings)
                AddLog($"DXF 경고: {warning}");
        }
        catch (OperationCanceledException)
        {
            DxfProgressText.Text = "로드 취소";
            AddLog("DXF 로드가 취소되었습니다.");
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "DXF 예외", MessageBoxButton.OK, MessageBoxImage.Error);
            AddLog($"DXF 예외: {exception.Message}");
        }
        finally
        {
            SetDxfLoading(false);
        }
    }

    /// <summary>
    /// DXF 파일을 읽고 좌표 및 경로를 변환할 때 사용할 기본 옵션을 생성합니다.
    /// 단위, 스케일, 회전, 곡선 정밀도 및 경로 최적화 조건을 지정합니다.
    /// </summary>
    private static DxfLoadOptions CreateDxfLoadOptions()
    {
        return new DxfLoadOptions
        {
            SourceUnitToMillimeter = 1.0,
            Scale = 1.0,
            RotationDegrees = 0.0,
            MirrorX = false,
            MirrorY = false,
            OffsetXMillimeter = 0.0,
            OffsetYMillimeter = 0.0,
            CurvePrecision = 128,
            SplinePrecision = 128,
            WeldToleranceMillimeter = 0.0001,
            ZToleranceMillimeter = 0.001,
            RejectNonPlanarEntities = true,
            IgnoreInvisibleEntities = true,
            IgnoreInvisibleLayers = true,
            IgnoreFrozenLayers = true,
            ExplodeInserts = true,
            OptimizeTravelOrder = false
        };
    }

    /// <summary>
    /// 새로운 DXF 파일을 불러오기 전에 이전 결과와 UI 상태를 초기화합니다.
    /// DXF 로드 중 상태로 전환하고 파일 경로를 로그에 기록합니다.
    /// </summary>
    private void PrepareDxfLoad(string filePath)
    {
        DxfPathTextBox.Text = filePath;
        DxfProgressBar.Value = 0;
        DxfProgressText.Text = "로드 시작";
        DxfResultText.Text = string.Empty;
        DxfCommandGrid.ItemsSource = null;
        _loadedDxf = null;
        _rtc6CommandStore.Clear();
        _rtc6ListWriter.InvalidateWrittenList();
        StartListButton.IsEnabled = false;

        SetDxfLoading(true);
        AddLog($"DXF 로드 시작: {filePath}");
    }

    /// <summary>
    /// DXF 로드 작업에서 전달된 진행 정보를 진행률 표시줄과 텍스트에 표시합니다.
    /// </summary>
    private void UpdateDxfProgress(DxfLoadProgress progress)
    {
        DxfProgressBar.Value = progress.Total == 0
            ? 0
            : (double)progress.Current / progress.Total * 100.0;

        DxfProgressText.Text = $"{progress.Current} / {progress.Total} ({progress.EntityType})";
    }

    /// <summary>
    /// DXF 로드 결과의 버전, 단위, Entity 수, 경로 길이 및 좌표 범위를 화면에 표시합니다.
    /// </summary>
    private void ShowDxfResult(DxfLoadResult result)
    {
        string boundsText = result.Bounds is DxfPathBounds bounds
            ? $"범위: ({bounds.MinimumX:F3}, {bounds.MinimumY:F3}) ~ " +
              $"({bounds.MaximumX:F3}, {bounds.MaximumY:F3}) / " +
              $"크기: {bounds.Width:F3} × {bounds.Height:F3} mm"
            : "범위 정보 없음";

        DxfResultText.Text =
            $"버전: {result.Version}  |  단위: {result.SourceDrawingUnit}  |  " +
            $"Entity: {result.Statistics.TotalEntityCount}  |  " +
            $"Contour: {result.Statistics.ContourCount}  |  " +
            $"Point: {result.Statistics.PointCount}  |  " +
            $"Command: {result.Commands.Count}\n" +
            $"Mark 길이: {result.Statistics.TotalMarkLengthMillimeter:F3} mm  |  " +
            $"Jump 길이: {result.Statistics.EstimatedJumpLengthMillimeter:F3} mm\n" +
            boundsText;
    }

    /// <summary>
    /// DXF에서 생성된 Jump 및 Mark 명령을 DataGrid에 표시합니다.
    /// 각 명령의 순번, 종류, 좌표 및 레이어 이름을 출력합니다.
    /// </summary>
    private void ShowDxfCommands(DxfLoadResult result)
    {
        DxfCommandGrid.ItemsSource = result.Commands.Select((command, index) => new
        {
            Index = index + 1,
            command.Type,
            command.X,
            command.Y,
            command.LayerName
        }).ToList();
    }

    /// <summary>
    /// 현재 로드된 mm 단위 DXF 이동 명령을 중심 이동 후 RTC6 정수 좌표 명령으로 변환하고
    /// 변환 결과를 Rtc6CommandStore에 저장합니다.
    /// 변환 중 오류가 발생하면 기존 RTC6 명령 목록을 삭제합니다.
    /// </summary>
    private bool PrepareRtc6Commands()
    {
        if (_loadedDxf is null || !_loadedDxf.Success)
        {
            _rtc6CommandStore.Clear();
            AddLog("RTC6 명령으로 변환할 DXF 데이터가 없습니다.");
            return false;
        }

        if (_loadedDxf.Commands.Count == 0)
        {
            _rtc6CommandStore.Clear();
            AddLog("RTC6 명령으로 변환할 DXF 이동 명령이 없습니다.");
            return false;
        }

        try
        {
            Rtc6CommandBuilder commandBuild = new();

            // Rtc6정수 좌표 생성
            List<Rtc6MotionCommand> rtc6MotionCommands = commandBuild.BuildRtc6Commands(_loadedDxf.Commands, _systemSettings, _modelSettings);

            // Rtc6정수 좌표 List 저장
            _rtc6CommandStore.Replace(rtc6MotionCommands);

            return true;
        }
        catch (Exception exception)
        {
            _rtc6CommandStore.Clear();

            string message = "RTC6 이동 명령 변환 중 오류가 발생했습니다.\n" + $"내용: {exception.Message}";

            AddLog(message);

            MessageBox.Show(
                message,
                "RTC6 좌표 변환 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
    }

    /// <summary>
    /// 현재 실행 중인 DXF 비동기 로드 작업에 취소를 요청합니다.
    /// </summary>
    private void CancelDxfLoadButton_Click(object sender, RoutedEventArgs e)
    {
        _dxfLoadCts?.Cancel();
    }

    /// <summary>
    /// DXF 로드 상태에 따라 파일 열기 버튼과 취소 버튼의 활성 상태를 변경합니다.
    /// </summary>
    private void SetDxfLoading(bool isLoading)
    {
        OpenDxfButton.IsEnabled = !isLoading;
        CancelDxfLoadButton.IsEnabled = isLoading;
    }

    /// <summary>
    /// 시스템 설정에 지정된 RTC6 폴더와 파일을 검사한 후 RTC6를 초기화합니다.
    /// Simulation 체크 상태에 따라 실제 장비 모드 또는 시뮬레이션 모드로 실행합니다.
    /// </summary>
    private void InitializeButton_Click(object sender, RoutedEventArgs e)
    {
        _rtc6ExecutionCts?.Cancel();
        _rtc6ListWriter.InvalidateWrittenList();
        StartListButton.IsEnabled = false;

        bool simulationMode = SimulationCheckBox.IsChecked == true;
        string rtc6FolderPath = string.Empty;
        string correctionFilePath = string.Empty;

        if (!simulationMode && !_systemSettingsService.TryResolveRtc6Paths(
            _systemSettings,
            out rtc6FolderPath,
            out correctionFilePath))
        {
            MessageBox.Show(
                _systemSettingsService.LastError,
                "RTC6 파일 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (!_rtc6.Initialize(
            _systemSettings.BoardNumber,
            rtc6FolderPath,
            correctionFilePath,
            simulationMode))
        { 
            StatusText.Text = "Error";
            AddLog(_rtc6.LastError);

            MessageBox.Show(_rtc6.LastError, "RTC6 초기화 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // RTC6 초기화가 성공한 뒤 SystemSettings 값을 보드에 적용합니다.
        if (!_rtc6SettingsApplier.Apply(_systemSettings))
        {
            AddLog(_rtc6SettingsApplier.LastError);

            MessageBox.Show(_rtc6SettingsApplier.LastError, "RTC6 설정 적용 오류", MessageBoxButton.OK, MessageBoxImage.Error);

            _rtc6.Shutdown();
            return;
        }

        StatusText.Text = _rtc6.IsSimulationMode ? "Simulation" : "Ready";

        AddLog(_rtc6.IsSimulationMode ? "RTC6 시뮬레이션 모드 초기화 완료" : "RTC6 실제 장비 초기화 완료");
    }

    /// <summary>
    /// 프로그램 시작 시 system.json을 읽어 현재 시스템 설정값에 적용합니다.
    /// 설정 파일을 읽지 못하면 오류를 표시하고 RTC6 초기화 버튼을 비활성화합니다.
    /// </summary>
    private void LoadSystemSettings()
    {
        if (_systemSettingsService.Load(out SystemSettings loadedSettings))
        {
            _systemSettings = loadedSettings;
            return;
        }

        MessageBox.Show(
            _systemSettingsService.LastError,
            "설정 불러오기 오류",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        InitializeButton.IsEnabled = false;
    }

    /// <summary>
    /// 화면에 입력된 X, Y 좌표와 지정된 속도로 RTC6 Jump 이동을 실행합니다.
    /// 좌표 입력이 숫자가 아니거나 이동에 실패하면 오류를 표시합니다.
    /// </summary>
    private void MoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(XTextBox.Text, out int x) || !int.TryParse(YTextBox.Text, out int y))
        {
            MessageBox.Show("X와 Y에 숫자를 입력하세요.");
            return;
        }

        const double speed = 1000.0;

        if (_rtc6.MoveTo(x, y, speed))
        {
            AddLog($"이동 실행: X={x}, Y={y}, Speed={speed}");
            return;
        }

        AddLog(_rtc6.LastError);
        MessageBox.Show(_rtc6.LastError);
    }

    /// <summary>
    /// RTC6 List 1이 작성 완료된 상태인지 확인한 후 execute_list 명령을 전송하고 실제 스캔 출력이 끝날 때까지 상태를 감시합니다.
    /// </summary>
    private async void StartListButton_Click(object sender, RoutedEventArgs e)
    {
        const uint listNumber = 1;

        if (!_rtc6ListExecutor.Start(listNumber))
        {
            AddLog(_rtc6ListExecutor.LastError);
            MessageBox.Show(_rtc6ListExecutor.LastError, "RTC6 List 실행 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _rtc6ExecutionCts?.Cancel();
        _rtc6ExecutionCts?.Dispose();
        _rtc6ExecutionCts = new CancellationTokenSource();

        SetRtc6Executing(true);
        StatusText.Text = _rtc6.IsSimulationMode ? "Simulation Running" : "Running";
        AddLog($"RTC6 List {listNumber} 실행 시작");

        try
        {
            bool completed = await _rtc6ListExecutor.WaitForCompletionAsync(20, _rtc6ExecutionCts.Token);

            if (!completed)
            {
                string statusError = _rtc6ListExecutor.LastError;
                _rtc6ListExecutor.Stop();
                AddLog(statusError);
                MessageBox.Show(statusError, "RTC6 상태 확인 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusText.Text = _rtc6.IsSimulationMode ? "Simulation" : "Ready";
            AddLog($"RTC6 List {listNumber} 실행 완료: OutputPosition={_rtc6ListExecutor.LastStatus.OutputPosition}");
        }
        catch (OperationCanceledException)
        {
            AddLog($"RTC6 List {listNumber} 실행 상태 감시 종료");
        }
        finally
        {
            SetRtc6Executing(false);
            _rtc6ExecutionCts?.Dispose();
            _rtc6ExecutionCts = null;
        }
    }

    /// <summary>
    /// RTC6 List 실행 상태에 따라 Start, DXF, 초기화, 설정 및 수동 이동 버튼의 활성 상태를 변경합니다.
    /// </summary>
    private void SetRtc6Executing(bool isExecuting)
    {
        StartListButton.IsEnabled = !isExecuting && _rtc6.IsInitialized && _rtc6ListWriter.LastWrittenListNumber == 1 && _rtc6ListWriter.LastWrittenCommandCount > 0;
        OpenDxfButton.IsEnabled = !isExecuting;
        InitializeButton.IsEnabled = !isExecuting;
        OpenSystemSettingsButton.IsEnabled = !isExecuting;
        MoveButton.IsEnabled = !isExecuting;
        SimulationCheckBox.IsEnabled = !isExecuting;
    }

    /// <summary>
    /// RTC6에서 현재 실행 중인 List 및 스캐너 이동을 정지합니다.
    /// </summary>
    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rtc6.Stop())
            AddLog("RTC6 정지 명령 실행");
        else
            AddLog(_rtc6.LastError);
    }

    /// <summary>
    /// RTC6 실행을 정지하고 DLL 및 연결 상태를 해제합니다.
    /// </summary>
    private void ShutdownButton_Click(object sender, RoutedEventArgs e)
    {
        _rtc6ExecutionCts?.Cancel();
        _rtc6ListWriter.InvalidateWrittenList();
        StartListButton.IsEnabled = false;
        _rtc6.Shutdown();
        StatusText.Text = "연결 안 됨";
        AddLog("RTC6 연결 종료");
    }

    /// <summary>
    /// 메인 창이 닫힐 때 실행 중인 DXF 작업을 취소하고 RTC6 자원을 해제합니다.
    /// </summary>
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _dxfLoadCts?.Cancel();
        _dxfLoadCts?.Dispose();
        _rtc6.Dispose();
    }

    /// <summary>
    /// 현재 시간을 포함한 메시지를 로그 TextBox에 추가하고 마지막 줄로 스크롤합니다.
    /// </summary>
    private void AddLog(string message)
    {
        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    }
}