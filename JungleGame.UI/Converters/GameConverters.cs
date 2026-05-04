using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JungleGame.UI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (bool)value ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        (Visibility)value == Visibility.Visible;
}

public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        !(bool)value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        !(bool)value;
}

public class GameStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = (JungleGame.Core.Model.GameStatus)value;
        return status switch
        {
            JungleGame.Core.Model.GameStatus.BlueWins => "\U0001F3C6 Blue Wins! \U0001F3C6",
            JungleGame.Core.Model.GameStatus.RedWins => "\U0001F3C6 Red Wins! \U0001F3C6",
            _ => ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
