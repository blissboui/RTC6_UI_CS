using Microsoft.Win32;
using RTC6_UI.Dxf;
using RTC6_UI.Dxf.Models;
using RTC6_UI.Services;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;

// ============================================================
// 파일: MainWindow.xaml.cs
// 역할: RTC6 제어 및 DXF 로드 처리
// ============================================================

namespace RTC6_UI;

public partial class MainWindow : Window
{
    private readonly Rtc6Controller _rtc6 = new();
    private readonly DxfLoader _dxfLoader = new();

    private CancellationTokenSource? _dxfLoadCts;
    private DxfLoadResult? _loadedDxf;

    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;
    }

    private async void OpenDxfButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "DXF 파일 선택",
            Filter = "DXF 파일 (*.dxf)|*.dxf|모든 파일 (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true) return;

        _dxfLoadCts?.Cancel();  // ?. : Null이 아닐 시 호출
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
                MessageBox.Show(result.ErrorMessage, "DXF 오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                AddLog($"DXF 로드 실패: {result.ErrorMessage}");
                return;
            }

            _loadedDxf = result;

            ShowDxfResult(result);
            ShowDxfCommands(result);

            DxfProgressBar.Value = 100;
            DxfProgressText.Text = "로드 완료";

            AddLog($"DXF 로드 완료: Contour {result.Contours.Count}개, Command {result.Commands.Count}개");

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
            MessageBox.Show(exception.Message, "DXF 예외",
                MessageBoxButton.OK, MessageBoxImage.Error);

            AddLog($"DXF 예외: {exception.Message}");
        }
        finally
        {
            SetDxfLoading(false);
        }
    }

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

    private void PrepareDxfLoad(string filePath)
    {
        DxfPathTextBox.Text = filePath;
        DxfProgressBar.Value = 0;
        DxfProgressText.Text = "로드 시작";
        DxfResultText.Text = string.Empty;
        DxfCommandGrid.ItemsSource = null;
        _loadedDxf = null;

        SetDxfLoading(true);
        AddLog($"DXF 로드 시작: {filePath}");
    }

    private void UpdateDxfProgress(DxfLoadProgress progress)
    {
        DxfProgressBar.Value = progress.Total == 0
            ? 0
            : (double)progress.Current / progress.Total * 100.0;

        DxfProgressText.Text = $"{progress.Current} / {progress.Total} ({progress.EntityType})";
    }

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

    private void CancelDxfLoadButton_Click(object sender, RoutedEventArgs e)
    {
        _dxfLoadCts?.Cancel();
    }

    private void SetDxfLoading(bool isLoading)
    {
        OpenDxfButton.IsEnabled = !isLoading;
        CancelDxfLoadButton.IsEnabled = isLoading;
    }

    private void InitializeButton_Click(object sender, RoutedEventArgs e)
    {
        string programFolderPath = @"C:\Users\boboy\Desktop\RTC6-1.24.0\RTC6 Files\Program Files";

        string correctionFilePath = @"C:\Users\boboy\Desktop\RTC6-1.24.0\Correction Files\실제파일명.ct5";

        bool success = _rtc6.Initialize(
            boardNumber: 1,
            programFolderPath: programFolderPath,
            correctionFilePath: correctionFilePath,
            simulationMode: SimulationCheckBox.IsChecked == true);

        if (!success)
        {
            StatusText.Text = "Error";
            AddLog(_rtc6.LastError);

            MessageBox.Show(_rtc6.LastError, "RTC6 초기화 오류",
                MessageBoxButton.OK, MessageBoxImage.Error);

            return;
        }

        StatusText.Text = _rtc6.IsSimulationMode ? "Simulation" : "Ready";
        AddLog(_rtc6.IsSimulationMode
            ? "RTC6 시뮬레이션 모드 초기화 완료"
            : "RTC6 실제 장비 초기화 완료");
    }

    private void MoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(XTextBox.Text, out int x) ||
            !int.TryParse(YTextBox.Text, out int y))
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

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rtc6.Stop())
            AddLog("RTC6 정지 명령 실행");
        else
            AddLog(_rtc6.LastError);
    }

    private void ShutdownButton_Click(object sender, RoutedEventArgs e)
    {
        _rtc6.Shutdown();
        StatusText.Text = "연결 안 됨";
        AddLog("RTC6 연결 종료");
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _dxfLoadCts?.Cancel();
        _dxfLoadCts?.Dispose();
        _rtc6.Dispose();
    }

    private void AddLog(string message)
    {
        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    }
}