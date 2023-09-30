#if false
using System.Globalization;
using System.Windows.Data;

namespace WpfAutoCompletePathTextBox;

[ValueConversion(typeof(int), typeof(int))]
public sealed class InvertSignConverter : IValueConverter
{
    public static InvertSignConverter Shared { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return -d;

        throw new NotSupportedException();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
#endif
