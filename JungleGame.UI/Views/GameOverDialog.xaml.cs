using System.Windows;
using JungleGame.Core.Model;

namespace JungleGame.UI.Views;

public partial class GameOverDialog : Window
{
    public GameOverDialog(GameStatus status)
    {
        InitializeComponent();
        if (status == GameStatus.BlueWins)
            ResultText.Text = "Blue Wins!";
        else if (status == GameStatus.RedWins)
            ResultText.Text = "Red Wins!";
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
