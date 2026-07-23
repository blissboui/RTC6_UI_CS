using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RTC6_UI.Settings
{
    /// <summary>
    /// 커팅 라인과 파크 위치를 계산할 때 사용할 기준 위치입니다.
    /// </summary>
    public enum CuttingReferencePoint
    {
        /// <summary>
        /// 좌표축의 음의 방향 기준입니다. UI에서는 바닥 또는 좌측으로 표시합니다.
        /// </summary>
        NegativeSide,

        /// <summary>
        /// 좌표축의 중심 위치를 기준으로 사용합니다.
        /// </summary>
        Center,

        /// <summary>
        /// 좌표축의 양의 방향 기준입니다. UI에서는 상단 또는 우측으로 표시합니다.
        /// </summary>
        PositiveSide
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
    /// 제품 또는 도안별로 사용하는 공정 파라미터를 저장합니다.
    /// 마킹 속도, 패턴 배치, 롤 길이, 커팅 위치 및 OTF 경로 설정을 포함합니다.
    /// </summary>
    public sealed class ModelSettings : INotifyPropertyChanged
    {
        private double _markingSpeedMillimeterPerSecond = 2500.0;
        private double _jumpSpeedMillimeterPerSecond = 10000.0;
        private double _patternPitchMillimeter = 8.0;
        private double _rollTotalLengthMillimeter = 5050.0;
        private double _frontCutLengthMillimeter = 605.0;
        private double _rearCutLengthMillimeter = 237.0;
        private CuttingReferencePoint _cuttingReference = CuttingReferencePoint.NegativeSide;
        private double _cuttingLineOffsetMillimeter;
        private double _parkOffsetMillimeter = 5.0;
        private double _patternOffsetXMillimeter;
        private double _patternOffsetYMillimeter;
        private double _patternScaleX = 1.0;
        private double _patternScaleY = 1.0;
        private double _materialFeedSpeedMillimeterPerSecond = 1000.0;
        private OtfPathMode _pathMode = OtfPathMode.FeedOptimized;
        private bool _allowStrokeReverse = true;
        private bool _allowContinuousBoundaryMark;

        /// <summary>
        /// 속성값이 변경되었음을 바인딩된 UI에 알리는 이벤트입니다.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 현재 모델 설정값의 복사본을 생성합니다.
        /// 설정창에서 취소했을 때 원본 설정이 변경되지 않도록 사용합니다.
        /// </summary>
        public ModelSettings Clone()
        {
            ModelSettings clone = (ModelSettings)MemberwiseClone();
            clone.PropertyChanged = null;
            return clone;
        }

        /// <summary>
        /// 레이저가 Mark 경로를 따라 이동할 때 사용하는 마킹 속도입니다.
        /// 단위는 mm/s입니다.
        /// </summary>
        public double MarkingSpeedMillimeterPerSecond
        {
            get => _markingSpeedMillimeterPerSecond;
            set => SetField(ref _markingSpeedMillimeterPerSecond, value);
        }

        /// <summary>
        /// 레이저 출력 없이 Jump 이동할 때 사용하는 속도입니다.
        /// 단위는 mm/s입니다.
        /// </summary>
        public double JumpSpeedMillimeterPerSecond
        {
            get => _jumpSpeedMillimeterPerSecond;
            set => SetField(ref _jumpSpeedMillimeterPerSecond, value);
        }

        /// <summary>
        /// 반복 패턴 사이의 이송 간격입니다.
        /// 단위는 mm입니다.
        /// </summary>
        public double PatternPitchMillimeter
        {
            get => _patternPitchMillimeter;
            set => SetField(ref _patternPitchMillimeter, value);
        }

        /// <summary>
        /// 롤 한 본의 전체 길이입니다.
        /// 단위는 mm입니다.
        /// </summary>
        public double RollTotalLengthMillimeter
        {
            get => _rollTotalLengthMillimeter;
            set => SetField(ref _rollTotalLengthMillimeter, value);
        }

        /// <summary>
        /// 롤 앞부분에서 패턴 가공 전에 확보할 직선 커팅 구간입니다.
        /// 단위는 mm입니다.
        /// </summary>
        public double FrontCutLengthMillimeter
        {
            get => _frontCutLengthMillimeter;
            set => SetField(ref _frontCutLengthMillimeter, value);
        }

        /// <summary>
        /// 롤 뒷부분에서 패턴 가공 후 확보할 직선 커팅 구간입니다.
        /// 단위는 mm입니다.
        /// </summary>
        public double RearCutLengthMillimeter
        {
            get => _rearCutLengthMillimeter;
            set => SetField(ref _rearCutLengthMillimeter, value);
        }

        /// <summary>
        /// 커팅 라인과 파크 위치의 절대좌표를 계산할 기준 위치입니다.
        /// </summary>
        public CuttingReferencePoint CuttingReference
        {
            get => _cuttingReference;
            set => SetField(ref _cuttingReference, value);
        }

        /// <summary>
        /// 선택한 커팅 기준점에서 커팅 라인까지의 거리입니다.
        /// 단위는 mm입니다.
        /// </summary>
        public double CuttingLineOffsetMillimeter
        {
            get => _cuttingLineOffsetMillimeter;
            set => SetField(ref _cuttingLineOffsetMillimeter, value);
        }

        /// <summary>
        /// 선택한 커팅 기준점에서 스캐너 파크 위치까지의 거리입니다.
        /// 단위는 mm입니다.
        /// </summary>
        public double ParkOffsetMillimeter
        {
            get => _parkOffsetMillimeter;
            set => SetField(ref _parkOffsetMillimeter, value);
        }

        /// <summary>
        /// 중심 정렬된 패턴을 X축 방향으로 추가 이동할 거리입니다.
        /// 단위는 mm입니다.
        /// </summary>
        public double PatternOffsetXMillimeter
        {
            get => _patternOffsetXMillimeter;
            set => SetField(ref _patternOffsetXMillimeter, value);
        }

        /// <summary>
        /// 중심 정렬된 패턴을 Y축 방향으로 추가 이동할 거리입니다.
        /// 단위는 mm입니다.
        /// </summary>
        public double PatternOffsetYMillimeter
        {
            get => _patternOffsetYMillimeter;
            set => SetField(ref _patternOffsetYMillimeter, value);
        }

        /// <summary>
        /// 패턴 중심을 기준으로 적용할 X축 배율입니다.
        /// 1.0이면 원래 크기입니다.
        /// </summary>
        public double PatternScaleX
        {
            get => _patternScaleX;
            set => SetField(ref _patternScaleX, value);
        }

        /// <summary>
        /// 패턴 중심을 기준으로 적용할 Y축 배율입니다.
        /// 1.0이면 원래 크기입니다.
        /// </summary>
        public double PatternScaleY
        {
            get => _patternScaleY;
            set => SetField(ref _patternScaleY, value);
        }

        /// <summary>
        /// 롤 또는 소재가 이동하는 속도입니다.
        /// 단위는 mm/s입니다.
        /// </summary>
        public double MaterialFeedSpeedMillimeterPerSecond
        {
            get => _materialFeedSpeedMillimeterPerSecond;
            set => SetField(ref _materialFeedSpeedMillimeterPerSecond, value);
        }

        /// <summary>
        /// 반복 패턴 사이의 Jump 이동 경로 최적화 방식을 지정합니다.
        /// </summary>
        public OtfPathMode PathMode
        {
            get => _pathMode;
            set => SetField(ref _pathMode, value);
        }

        /// <summary>
        /// 반복 패턴에서 경로 진행 방향을 반대로 실행할 수 있는지 나타냅니다.
        /// </summary>
        public bool AllowStrokeReverse
        {
            get => _allowStrokeReverse;
            set => SetField(ref _allowStrokeReverse, value);
        }

        /// <summary>
        /// 이전 패턴의 마지막 Mark와 다음 패턴의 첫 Mark를 연속 연결할지 나타냅니다.
        /// </summary>
        public bool AllowContinuousBoundaryMark
        {
            get => _allowContinuousBoundaryMark;
            set => SetField(ref _allowContinuousBoundaryMark, value);
        }

        /// <summary>
        /// 필드값을 변경하고 바인딩된 UI에 속성 변경을 알립니다.
        /// 기존 값과 새로운 값이 같으면 변경 알림을 발생시키지 않습니다.
        /// </summary>
        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}