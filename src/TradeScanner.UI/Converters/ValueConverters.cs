using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TradeScanner.UI.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

[ValueConversion(typeof(bool), typeof(string))]
public class BoolToScanLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "Stop Scanning" : "Start Scanning";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(bool), typeof(Brush))]
public class BoolToGreenRedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? new SolidColorBrush(Color.FromRgb(0x26, 0xA6, 0x9A))
                      : new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(decimal), typeof(Brush))]
public class ChangeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return d >= 0
                ? new SolidColorBrush(Color.FromRgb(0x26, 0xA6, 0x9A))
                : new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(int), typeof(Brush))]
public class ScoreToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int score)
        {
            if (score >= 75) return new SolidColorBrush(Color.FromRgb(0x26, 0xA6, 0x9A));
            if (score >= 50) return new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x00));
            return new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(bool), typeof(string))]
public class BoolToStreamLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "Stop Stream" : "Stream";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(bool), typeof(string))]
public class BoolToAvailableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "Available" : "No API Key";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(string), typeof(Brush))]
public class ValidationToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Valid" => new SolidColorBrush(Color.FromRgb(0x26, 0xA6, 0x9A)),
            "Invalid" => new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
