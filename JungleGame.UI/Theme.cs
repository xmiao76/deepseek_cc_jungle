using System.Windows;
using System.Windows.Media;

namespace JungleGame.UI;

/// <summary>
/// Access to the brushes defined in Themes/Theme.xaml from code-behind board
/// rendering. Returns a magenta brush when a key is missing so a typo shows up
/// visually instead of throwing mid-render.
/// </summary>
public static class Theme
{
    public static Brush GetBrush(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? MissingBrush;

    public static Color GetColor(string key) =>
        (Application.Current.TryFindResource(key) as SolidColorBrush)?.Color ?? Colors.Magenta;

    private static readonly SolidColorBrush MissingBrush = new(Colors.Magenta);
}
