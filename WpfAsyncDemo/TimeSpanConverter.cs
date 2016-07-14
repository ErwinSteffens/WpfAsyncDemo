using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfAsyncDemo
{
    public class TimeSpanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is TimeSpan))
                return DependencyProperty.UnsetValue;
            var timeSpan = (TimeSpan)value;
            if (timeSpan < TimeSpan.Zero)
                return DependencyProperty.UnsetValue;
            return Math.Round(timeSpan.TotalSeconds).ToString(culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            if (!string.IsNullOrEmpty(text))
            {
                int seconds;
                if (int.TryParse(text, out seconds))
                    return TimeSpan.FromSeconds(seconds);
            }
            return TimeSpan.MinValue;
        }
    }
}
