using System;
using System.Windows;
using RTC6_UI.Services;
using RTC6_UI.Settings;

namespace RTC6_UI
{
    /// <summary>
    /// 사용자가 RTC6 하드웨어 및 OTF 시스템 설정값을 입력하는 설정창입니다.
    /// 확인을 누르면 입력값을 검사하여 ResultSettings로 반환하고,
    /// 취소를 누르면 기존 설정을 변경하지 않습니다.
    /// </summary>
    public partial class SystemSettingsWindow : Window
    {
        /// <summary>
        /// 설정창에서 편집 중인 시스템 설정 복사본입니다.
        /// </summary>
        public SystemSettings EditingSettings { get; }

        /// <summary>
        /// 사용자가 확인한 최종 시스템 설정입니다.
        /// 취소한 경우 null입니다.
        /// </summary>
        public SystemSettings? ResultSettings { get; private set; }

        /// <summary>
        /// 기존 설정값을 복사하여 설정창의 초기값으로 표시합니다.
        /// </summary>
        public SystemSettingsWindow(SystemSettings currentSettings)
        {
            InitializeComponent();

            EditingSettings = currentSettings.Clone();
            DataContext = EditingSettings;

            InitializeComboBoxes();
            UpdateDelayControlState();
        }

        /// <summary>
        /// RTC6 프로그램 파일이 들어 있는 폴더를 선택하고 설정값에 반영합니다.
        /// </summary>
        private void BrowseProgramFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFolderDialog dialog = new()
            {
                Title = "RTC6 프로그램 폴더 선택",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
                return;

            EditingSettings.Rtc6FilesFolder = dialog.FolderName;
        }

        /// <summary>
        /// ComboBox에 enum 항목을 등록합니다.
        /// </summary>
        private void InitializeComboBoxes()
        {
            FeedAxisComboBox.ItemsSource = Enum.GetValues<FeedAxis>();
            FeedDirectionComboBox.ItemsSource = Enum.GetValues<FeedDirection>();
            EncoderPolarityComboBox.ItemsSource = Enum.GetValues<EncoderPolarity>();
            OperationModeComboBox.ItemsSource = Enum.GetValues<OperationMode>();
            LaserModeComboBox.ItemsSource = Enum.GetValues<LaserMode>();
            PathModeComboBox.ItemsSource = Enum.GetValues<OtfPathMode>();
            MotionCompensationComboBox.ItemsSource = Enum.GetValues<MotionCompensationMode>();
            FlyActivationComboBox.ItemsSource = Enum.GetValues<FlyActivationMode>();
        }

        /// <summary>
        /// UI에 입력된 설정값을 검사하고 최종 설정으로 반환합니다.
        /// </summary>
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SystemSettingsValidator.Validate(EditingSettings, out string error))
            {
                MessageBox.Show(error, "설정값 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Rtc6FileValidator.TryValidate(EditingSettings.Rtc6FilesFolder, EditingSettings.CorrectionFileName, out _, out _, out string fileError))
            {
                MessageBox.Show(fileError, "RTC6 파일 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResultSettings = EditingSettings.Clone();
            DialogResult = true;
        }

        /// <summary>
        /// 설정 변경을 취소하고 원본 설정을 유지합니다.
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ResultSettings = null;
            DialogResult = false;
        }

        /// <summary>
        /// Auto Delay 설정이 변경되면 수동 Delay 입력칸의 활성 상태를 갱신합니다.
        /// </summary>
        private void AutoDelayCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateDelayControlState();
        }

        /// <summary>
        /// Auto Delay가 꺼져 있을 때만 수동 Delay 입력칸을 활성화합니다.
        /// </summary>
        private void UpdateDelayControlState()
        {
            if (ManualDelayPanel is null)
                return;

            ManualDelayPanel.IsEnabled = !EditingSettings.UseAutoDelay;
        }
    }
}