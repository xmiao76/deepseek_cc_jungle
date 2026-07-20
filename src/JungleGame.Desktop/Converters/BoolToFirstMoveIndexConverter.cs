using System.Globalization;
using System.Windows.Data;

namespace JungleGame.Desktop.Converters;

/// <summary>
/// Converts IsHumanFirst (bool) to ComboBox SelectedIndex: true=0, false=1.
/// </summary>
public class BoolToFirstMoveIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isHumanFirst)
            return isHumanFirst ? 0 : 1;
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index)
            return index == 0;
        return true; // Default: human first
    }
}
