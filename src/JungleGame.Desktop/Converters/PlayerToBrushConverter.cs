using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using JungleGame.Core.Models;

namespace JungleGame.Desktop.Converters;

/// <summary>
/// Converts Player.Blue/Red to corresponding SolidColorBrush for UI display.
/// </summary>
public class PlayerToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush BlueBrush = new(Color.FromRgb(0x1A, 0x56, 0xDB));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(0xDB, 0x1A, 0x1A));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Player player)
        {
            return player == Player.Blue ? BlueBrush : RedBrush;
        }
        return BlueBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
