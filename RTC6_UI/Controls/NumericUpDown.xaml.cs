using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RTC6_UI.Controls
{
    /// <summary>
    /// 숫자를 직접 입력하거나 위·아래 버튼과 마우스 휠로 증감하는 입력 컨트롤입니다.
    /// 최소값, 최대값, 증감량 및 소수점 표시 자릿수를 설정할 수 있습니다.
    /// </summary>
    public partial class NumericUpDown : UserControl
    {
        /// <summary>
        /// 현재 숫자값을 나타내는 종속성 속성입니다.
        /// 기본적으로 양방향 바인딩을 사용합니다.
        /// </summary>
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(NumericUpDown),
            new FrameworkPropertyMetadata(
                0.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValuePropertyChanged,
                CoerceValue));

        /// <summary>
        /// 입력 가능한 최소값입니다.
        /// </summary>
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
            nameof(Minimum),
            typeof(double),
            typeof(NumericUpDown),
            new PropertyMetadata(double.MinValue, OnRangePropertyChanged));

        /// <summary>
        /// 입력 가능한 최대값입니다.
        /// </summary>
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum),
            typeof(double),
            typeof(NumericUpDown),
            new PropertyMetadata(double.MaxValue, OnRangePropertyChanged));

        /// <summary>
        /// 버튼을 한 번 클릭했을 때 변경되는 값입니다.
        /// </summary>
        public static readonly DependencyProperty IncrementProperty = DependencyProperty.Register(
            nameof(Increment),
            typeof(double),
            typeof(NumericUpDown),
            new PropertyMetadata(1.0));

        /// <summary>
        /// 화면에 표시할 소수점 자릿수입니다.
        /// </summary>
        public static readonly DependencyProperty DecimalPlacesProperty = DependencyProperty.Register(
            nameof(DecimalPlaces),
            typeof(int),
            typeof(NumericUpDown),
            new PropertyMetadata(0, OnDecimalPlacesChanged));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public double Increment
        {
            get => (double)GetValue(IncrementProperty);
            set => SetValue(IncrementProperty, value);
        }

        public int DecimalPlaces
        {
            get => (int)GetValue(DecimalPlacesProperty);
            set => SetValue(DecimalPlacesProperty, value);
        }

        /// <summary>
        /// 숫자 입력 컨트롤을 초기화하고 현재 값을 화면에 표시합니다.
        /// </summary>
        public NumericUpDown()
        {
            InitializeComponent();
            Loaded += (_, _) => UpdateDisplayedText();
        }

        /// <summary>
        /// 현재 값에 Increment를 더합니다.
        /// </summary>
        private void IncreaseButton_Click(object sender, RoutedEventArgs e)
        {
            CommitText();
            SetCurrentValue(ValueProperty, Math.Min(Maximum, Value + Increment));
        }

        /// <summary>
        /// 현재 값에서 Increment를 뺍니다.
        /// </summary>
        private void DecreaseButton_Click(object sender, RoutedEventArgs e)
        {
            CommitText();
            SetCurrentValue(ValueProperty, Math.Max(Minimum, Value - Increment));
        }

        /// <summary>
        /// Enter 키로 입력을 확정하고 위·아래 방향키로 값을 증감합니다.
        /// </summary>
        private void ValueTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitText();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                IncreaseButton_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                DecreaseButton_Click(sender, e);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 입력칸에서 포커스가 빠져나갈 때 입력값을 확정합니다.
        /// </summary>
        private void ValueTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CommitText();
        }

        /// <summary>
        /// 입력칸 위에서 마우스 휠을 움직이면 값을 증감합니다.
        /// </summary>
        private void ValueTextBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                IncreaseButton_Click(sender, e);
            else
                DecreaseButton_Click(sender, e);

            e.Handled = true;
        }

        /// <summary>
        /// TextBox 문자열을 숫자로 변환하고 입력 범위에 맞게 제한합니다.
        /// 잘못된 문자열이면 기존 값으로 되돌립니다.
        /// </summary>
        private void CommitText()
        {
            if (!double.TryParse(ValueTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value))
            {
                UpdateDisplayedText();
                return;
            }

            value = Math.Clamp(value, Minimum, Maximum);
            value = Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);

            SetCurrentValue(ValueProperty, value);
            UpdateDisplayedText();
        }

        /// <summary>
        /// 현재 값을 지정된 소수점 자릿수 형식으로 TextBox에 표시합니다.
        /// </summary>
        private void UpdateDisplayedText()
        {
            if (ValueTextBox is null)
                return;

            ValueTextBox.Text = Value.ToString($"F{Math.Max(0, DecimalPlaces)}", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Value가 변경되면 화면에 표시되는 문자열을 갱신합니다.
        /// </summary>
        private static void OnValuePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            ((NumericUpDown)dependencyObject).UpdateDisplayedText();
        }

        /// <summary>
        /// 최소값이나 최대값이 변경되면 현재 값을 새로운 범위 안으로 제한합니다.
        /// </summary>
        private static void OnRangePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            dependencyObject.CoerceValue(ValueProperty);
        }

        /// <summary>
        /// 소수점 자릿수가 변경되면 표시 문자열을 다시 생성합니다.
        /// </summary>
        private static void OnDecimalPlacesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            ((NumericUpDown)dependencyObject).UpdateDisplayedText();
        }

        /// <summary>
        /// Value가 최소값과 최대값 사이를 벗어나지 않도록 제한합니다.
        /// </summary>
        private static object CoerceValue(DependencyObject dependencyObject, object baseValue)
        {
            NumericUpDown control = (NumericUpDown)dependencyObject;
            double value = (double)baseValue;

            if (double.IsNaN(value) || double.IsInfinity(value))
                return control.Minimum;

            return Math.Clamp(value, control.Minimum, control.Maximum);
        }
    }
}