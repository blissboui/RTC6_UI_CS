using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RTC6_UI.Settings
{
    /// <summary>
    /// 소재가 이동하는 물리적인 축을 나타냅니다.
    /// </summary>
    public enum FeedAxis
    {
        X,
        Y
    }

    /// <summary>
    /// 소재가 이동하는 화면상의 방향을 나타냅니다.
    /// </summary>
    public enum FeedDirection
    {
        XMinus,
        XPlus,
        YMinus,
        YPlus
    }

    /// <summary>
    /// 엔코더 값이 이송 방향에서 증가하는지 감소하는지를 나타냅니다.
    /// </summary>
    public enum EncoderPolarity
    {
        Normal,
        Inverted
    }

    /// <summary>
    /// 레이저 출력 없이 검증하는 TEST 모드와 실제 운전용 Auto 모드를 구분합니다.
    /// </summary>
    public enum OperationMode
    {
        Test,
        Auto
    }

    /// <summary>
    /// RTC6에서 사용하는 레이저 제어 모드 이름을 나타냅니다.
    /// </summary>
    public enum LaserMode
    {
        Co2Gate = 0,
        Yag1 = 1,
        Yag2 = 2,
        Yag3 = 3,
        Laser4 = 4,
        Yag5 = 5,
        Laser6 = 6
    }

    /// <summary>
    /// 반복 패턴 사이의 Jump 이동 순서를 결정하는 OTF 경로 모드입니다.
    /// </summary>
    public enum OtfPathMode
    {
        Standard,
        FeedOptimized
    }

    /// <summary>
    /// 소재 이송에 따른 스캐너 위치 보정 방식을 나타냅니다.
    /// </summary>
    public enum MotionCompensationMode
    {
        Disabled,
        RtcFly
    }

    /// <summary>
    /// RTC6 Fly 기능의 활성화 방식을 나타냅니다.
    /// </summary>
    public enum FlyActivationMode
    {
        Disabled,
        SetFlyAxis
    }

    /// <summary>
    /// RTC6, 스캔 헤드, 엔코더, SCANahead, OTF 및 Delay에 관한
    /// 장비 공통 설정을 저장합니다.
    /// 이 값들은 system.json 파일에 저장합니다.
    /// </summary>
    public sealed class SystemSettings : INotifyPropertyChanged
    {
        private ushort _boardNumber = 1;
        private string _rtc6FilesFolder = "RTC6 Files";
        private string _correctionFileName = "Cor_1to1.ct5";
        private double _bitsPerMillimeter = 16644.0;
        private double _fieldSizeMillimeter = 63.0;
        private bool _flipScanX;
        private bool _flipScanY;
        private FeedAxis _feedAxis = FeedAxis.X;
        private FeedDirection _feedDirection = FeedDirection.XMinus;
        private bool _swapFlyAxis;
        private double _encoderPulsesPerMillimeter = 1000.0;
        private EncoderPolarity _encoderPolarity = EncoderPolarity.Normal;
        private uint _cornerScalePercent = 100;
        private uint _endScalePercent = 100;
        private uint _accScalePercent = 100;
        private int _laserShiftOn64;
        private int _laserShiftOff64;
        private OtfPathMode _pathMode = OtfPathMode.FeedOptimized;
        private bool _allowStrokeReverse = true;
        private bool _allowContinuousBoundaryMark;
        private bool _useFeedDirectionSkewCompensation;
        private double _feedDirectionSkewPercent;

        private MotionCompensationMode _motionCompensation =
            MotionCompensationMode.RtcFly;

        private FlyActivationMode _flyActivation =
            FlyActivationMode.SetFlyAxis;

        private double _flyScaleCorrectionPercent;
        private bool _invertFly;
        private OperationMode _operationMode = OperationMode.Test;
        private uint _donePortMask = 0x0001;
        private int _encoderTimeoutMilliseconds = 5000;
        private LaserMode _laserMode = LaserMode.Yag3;
        private double _laserFrequencyKilohertz = 50.0;
        private double _laserPulseWidthMicroseconds = 5.0;
        private bool _useAutoDelay = true;
        private int _laserOnDelay64 = 1280;
        private uint _laserOffDelay64 = 1320;
        private uint _scannerMarkDelayMicroseconds = 300;
        private uint _scannerJumpDelayMicroseconds = 300;
        private uint _scannerPolygonDelayMicroseconds = 100;

        /// <summary>
        /// 속성값이 변경되었음을 바인딩된 UI에 알리는 이벤트입니다.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 현재 시스템 설정값의 복사본을 생성합니다.
        /// 설정창에서 취소할 경우 원본 설정이 변경되지 않도록 사용할 수 있습니다.
        /// </summary>
        public SystemSettings Clone()
        {
            SystemSettings clone = (SystemSettings)MemberwiseClone();

            // 원본 설정 객체에 연결된 UI 이벤트는 복사하지 않습니다.
            clone.PropertyChanged = null;

            return clone;
        }

        /// <summary>
        /// 사용할 RTC6 보드 번호입니다.
        /// 첫 번째 보드는 일반적으로 1번입니다.
        /// </summary>
        public ushort BoardNumber
        {
            get => _boardNumber;
            set => SetField(ref _boardNumber, value);
        }

        /// <summary>
        /// RTC6DAT.dat와 RTC6OUT.out 파일이 있는 폴더입니다.
        /// 상대경로이면 실행 파일 폴더를 기준으로 처리합니다.
        /// </summary>
        public string Rtc6FilesFolder
        {
            get => _rtc6FilesFolder;
            set => SetField(ref _rtc6FilesFolder, value);
        }

        /// <summary>
        /// RTC6에 로드할 보정 파일 이름입니다.
        /// 기본값은 Cor_1to1.ct5이며 설정 UI에서 변경할 수 있습니다.
        /// </summary>
        public string CorrectionFileName
        {
            get => _correctionFileName;
            set => SetField(ref _correctionFileName, value);
        }

        /// <summary>
        /// mm 좌표를 RTC6 정수 좌표로 변환할 때 사용하는 비트 수입니다.
        /// 현재 장비 기준값은 16644 bit/mm입니다.
        /// </summary>
        public double BitsPerMillimeter
        {
            get => _bitsPerMillimeter;
            set => SetField(ref _bitsPerMillimeter, value);
        }

        /// <summary>
        /// 스캔 헤드가 사용할 수 있는 전체 필드 크기입니다.
        /// 좌표 원점을 중심으로 하면 범위는 ±FieldSize/2입니다.
        /// </summary>
        public double FieldSizeMillimeter
        {
            get => _fieldSizeMillimeter;
            set => SetField(ref _fieldSizeMillimeter, value);
        }

        /// <summary>
        /// 최종 RTC6 X 좌표의 부호를 반전합니다.
        /// </summary>
        public bool FlipScanX
        {
            get => _flipScanX;
            set => SetField(ref _flipScanX, value);
        }

        /// <summary>
        /// 최종 RTC6 Y 좌표의 부호를 반전합니다.
        /// </summary>
        public bool FlipScanY
        {
            get => _flipScanY;
            set => SetField(ref _flipScanY, value);
        }

        /// <summary>
        /// 소재가 이동하는 물리적인 축입니다.
        /// </summary>
        public FeedAxis FeedAxis
        {
            get => _feedAxis;
            set => SetField(ref _feedAxis, value);
        }

        /// <summary>
        /// 화면과 경로 계산에서 사용하는 소재 이송 방향입니다.
        /// </summary>
        public FeedDirection FeedDirection
        {
            get => _feedDirection;
            set => SetField(ref _feedDirection, value);
        }

        /// <summary>
        /// FeedAxis와 RTC6 set_fly_x 또는 set_fly_y의 매핑을 서로 교환합니다.
        /// </summary>
        public bool SwapFlyAxis
        {
            get => _swapFlyAxis;
            set => SetField(ref _swapFlyAxis, value);
        }

        /// <summary>
        /// 소재가 1mm 이동했을 때 증가하는 엔코더 펄스 수입니다.
        /// </summary>
        public double EncoderPulsesPerMillimeter
        {
            get => _encoderPulsesPerMillimeter;
            set => SetField(ref _encoderPulsesPerMillimeter, value);
        }

        /// <summary>
        /// 소재 이송 방향에 대한 엔코더 카운트 극성입니다.
        /// </summary>
        public EncoderPolarity EncoderPolarity
        {
            get => _encoderPolarity;
            set => SetField(ref _encoderPolarity, value);
        }

        /// <summary>
        /// SCANahead 코너 품질 스케일입니다.
        /// </summary>
        public uint CornerScalePercent
        {
            get => _cornerScalePercent;
            set => SetField(ref _cornerScalePercent, value);
        }

        /// <summary>
        /// SCANahead 선 끝부분 품질 스케일입니다.
        /// </summary>
        public uint EndScalePercent
        {
            get => _endScalePercent;
            set => SetField(ref _endScalePercent, value);
        }

        /// <summary>
        /// SCANahead 가감속 구간 품질 스케일입니다.
        /// </summary>
        public uint AccScalePercent
        {
            get => _accScalePercent;
            set => SetField(ref _accScalePercent, value);
        }

        /// <summary>
        /// SCANahead 자동 Delay에 추가할 레이저 ON 시점 보정값입니다.
        /// 단위는 1/64µs입니다.
        /// </summary>
        public int LaserShiftOn64
        {
            get => _laserShiftOn64;
            set => SetField(ref _laserShiftOn64, value);
        }

        /// <summary>
        /// SCANahead 자동 Delay에 추가할 레이저 OFF 시점 보정값입니다.
        /// 단위는 1/64µs입니다.
        /// </summary>
        public int LaserShiftOff64
        {
            get => _laserShiftOff64;
            set => SetField(ref _laserShiftOff64, value);
        }

        /// <summary>
        /// OTF 패턴 사이의 이동 경로 최적화 방식입니다.
        /// </summary>
        public OtfPathMode PathMode
        {
            get => _pathMode;
            set => SetField(ref _pathMode, value);
        }

        /// <summary>
        /// 교번 패턴에서 홀수 번째 경로를 반대 방향으로 실행할 수 있는지 나타냅니다.
        /// </summary>
        public bool AllowStrokeReverse
        {
            get => _allowStrokeReverse;
            set => SetField(ref _allowStrokeReverse, value);
        }

        /// <summary>
        /// 한 패턴의 마지막 Mark와 다음 패턴의 첫 Mark를 연속해서 연결할지 나타냅니다.
        /// </summary>
        public bool AllowContinuousBoundaryMark
        {
            get => _allowContinuousBoundaryMark;
            set => SetField(ref _allowContinuousBoundaryMark, value);
        }

        /// <summary>
        /// 이송 속도에 비례하는 선형 기울어짐 보정을 사용할지 나타냅니다.
        /// </summary>
        public bool UseFeedDirectionSkewCompensation
        {
            get => _useFeedDirectionSkewCompensation;
            set => SetField(
                ref _useFeedDirectionSkewCompensation,
                value);
        }

        /// <summary>
        /// 이송 방향 기울어짐 보정량입니다.
        /// </summary>
        public double FeedDirectionSkewPercent
        {
            get => _feedDirectionSkewPercent;
            set => SetField(ref _feedDirectionSkewPercent, value);
        }

        /// <summary>
        /// OTF 위치 보정 방식을 지정합니다.
        /// </summary>
        public MotionCompensationMode MotionCompensation
        {
            get => _motionCompensation;
            set => SetField(ref _motionCompensation, value);
        }

        /// <summary>
        /// Classic 1축 RTC6 Fly API 사용 여부를 지정합니다.
        /// </summary>
        public FlyActivationMode FlyActivation
        {
            get => _flyActivation;
            set => SetField(ref _flyActivation, value);
        }

        /// <summary>
        /// 기본 Fly Scale에 추가할 보정 비율입니다.
        /// 예를 들어 25를 입력하면 기본값에 1.25를 곱합니다.
        /// </summary>
        public double FlyScaleCorrectionPercent
        {
            get => _flyScaleCorrectionPercent;
            set => SetField(ref _flyScaleCorrectionPercent, value);
        }

        /// <summary>
        /// 엔코더 대기 방향은 유지하면서 Fly 보정 부호만 반전합니다.
        /// </summary>
        public bool InvertFly
        {
            get => _invertFly;
            set => SetField(ref _invertFly, value);
        }

        /// <summary>
        /// TEST 또는 Auto 운전 모드를 지정합니다.
        /// </summary>
        public OperationMode OperationMode
        {
            get => _operationMode;
            set => SetField(ref _operationMode, value);
        }

        /// <summary>
        /// 가공 완료 신호에 사용할 디지털 출력 비트 마스크입니다.
        /// </summary>
        public uint DonePortMask
        {
            get => _donePortMask;
            set => SetField(ref _donePortMask, value);
        }

        /// <summary>
        /// RTC6 엔코더 대기 명령의 최대 대기시간입니다.
        /// </summary>
        public int EncoderTimeoutMilliseconds
        {
            get => _encoderTimeoutMilliseconds;
            set => SetField(ref _encoderTimeoutMilliseconds, value);
        }

        /// <summary>
        /// 장비의 RTC6 레이저 제어 모드입니다.
        /// </summary>
        public LaserMode LaserMode
        {
            get => _laserMode;
            set => SetField(ref _laserMode, value);
        }

        /// <summary>
        /// 레이저 펄스 주파수 설정값입니다.
        /// </summary>
        public double LaserFrequencyKilohertz
        {
            get => _laserFrequencyKilohertz;
            set => SetField(ref _laserFrequencyKilohertz, value);
        }

        /// <summary>
        /// 레이저 펄스폭 설정값입니다.
        /// </summary>
        public double LaserPulseWidthMicroseconds
        {
            get => _laserPulseWidthMicroseconds;
            set => SetField(ref _laserPulseWidthMicroseconds, value);
        }

        /// <summary>
        /// SCANahead가 Laser 및 Scanner Delay를 자동 계산할지 나타냅니다.
        /// </summary>
        public bool UseAutoDelay
        {
            get => _useAutoDelay;
            set => SetField(ref _useAutoDelay, value);
        }

        /// <summary>
        /// 수동 레이저 ON Delay입니다.
        /// 단위는 1/64µs이며 음수값을 허용합니다.
        /// </summary>
        public int LaserOnDelay64
        {
            get => _laserOnDelay64;
            set => SetField(ref _laserOnDelay64, value);
        }

        /// <summary>
        /// 수동 레이저 OFF Delay입니다.
        /// 단위는 1/64µs입니다.
        /// </summary>
        public uint LaserOffDelay64
        {
            get => _laserOffDelay64;
            set => SetField(ref _laserOffDelay64, value);
        }

        /// <summary>
        /// 수동 Scanner Mark Delay입니다.
        /// UI에서 사용하는 단위는 µs입니다.
        /// </summary>
        public uint ScannerMarkDelayMicroseconds
        {
            get => _scannerMarkDelayMicroseconds;
            set => SetField(ref _scannerMarkDelayMicroseconds, value);
        }

        /// <summary>
        /// 수동 Scanner Jump Delay입니다.
        /// UI에서 사용하는 단위는 µs입니다.
        /// </summary>
        public uint ScannerJumpDelayMicroseconds
        {
            get => _scannerJumpDelayMicroseconds;
            set => SetField(ref _scannerJumpDelayMicroseconds, value);
        }

        /// <summary>
        /// 수동 Scanner Polygon Delay입니다.
        /// UI에서 사용하는 단위는 µs입니다.
        /// </summary>
        public uint ScannerPolygonDelayMicroseconds
        {
            get => _scannerPolygonDelayMicroseconds;
            set => SetField(ref _scannerPolygonDelayMicroseconds, value);
        }

        /// <summary>
        /// 필드값을 변경하고 바인딩된 UI에 속성 변경을 알립니다.
        /// 기존 값과 새로운 값이 같으면 변경 알림을 발생시키지 않습니다.
        /// </summary>
        private bool SetField<T>(
            ref T field,
            T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;

            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));

            return true;
        }
    }
}