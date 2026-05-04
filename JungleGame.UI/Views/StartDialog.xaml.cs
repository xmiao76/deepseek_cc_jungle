using System.Windows;
using System.Windows.Controls;

namespace JungleGame.UI.Views;

public partial class StartDialog : Window
{
    public bool HumanFirst { get; private set; } = true;
    public bool AiVsAi { get; private set; } = false;

    public StartDialog()
    {
        InitializeComponent();
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        HumanFirst = HumanFirstRadio.IsChecked == true;
        AiVsAi = AiVsAiCheck.IsChecked == true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
