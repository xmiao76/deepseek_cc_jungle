using System.Windows;
using JungleGame.Core.Model;

namespace JungleGame.UI.Views;

public partial class GameOverDialog : Window
{
    public GameOverDialog(GameStatus status)
    {
        InitializeComponent();
        ResultText.Text = status == GameStatus.Draw
            ? GameStrings.StatusText(status)
            : $"\U0001F3C6 {GameStrings.StatusText(status)} \U0001F3C6";

        // Winner accent strip: blue for Blue, red for Red, gold for a draw
        AccentStrip.Background = status switch
        {
            GameStatus.BlueWins => Theme.GetBrush("BluePieceBrush"),
            GameStatus.RedWins => Theme.GetBrush("RedPieceBrush"),
            _ => Theme.GetBrush("GoldBrush")
        };
        ResultText.Foreground = status == GameStatus.Draw
            ? Theme.GetBrush("TextSecondaryBrush")
            : Theme.GetBrush("GoldBrush");
    }

    private void NewGame_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
