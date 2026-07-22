using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RTC6_UI.Converters
{
    /// <summary>
    /// uint 값을 0x0000 형식으로 표시하고,
    /// 사용자가 입력한 문자열을 16진수 값으로 변환합니다.
    /// </summary>
    public sealed class HexUInt32Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is uint number ? $"0x{number:X4}" : "0x0000";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value?.ToString()?.Trim() ?? string.Empty;

            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text[2..];

            if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint result))
                return DependencyProperty.UnsetValue;

            if (result > 0xFFFF)
                return DependencyProperty.UnsetValue;

            return result;
        }
    }
}