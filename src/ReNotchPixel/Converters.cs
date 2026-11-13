using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ReNotchPixel;

public class PlayPauseConverter : IValueConverter
{
    public static readonly PlayPauseConverter Instance = new PlayPauseConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is string paramStr && paramStr.Contains(':'))
        {
            var parts = paramStr.Split(':');
            bool condition = false;
            if (value is bool b) condition = b;
            return condition ? parts[0] : parts[1];
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CollectionEmptyConverter : IValueConverter
{
    public static readonly CollectionEmptyConverter Instance = new CollectionEmptyConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int count = 0;
        if (value is int i) count = i;
        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
