using System.Windows;
using System.Windows.Controls;

namespace JungleGame.UI.Views;

public partial class StartDialog : Window
{
    public bool HumanFirst { get; private set; } = true;
    public bool AiVsAi { get; private set; }
    public int AiTimeMs { get; private set; } = 1000; // Medium

    public StartDialog()
    {
        InitializeComponent();
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        HumanFirst = HumanFirstRadio.IsChecked == true;
        AiVsAi = AiVsAiCheck.IsChecked == true;
        // Hard caps at 2 seconds per move; the other levels scale down accordingly
        AiTimeMs = HardRadio.IsChecked == true ? 2000
            : EasyRadio.IsChecked == true ? 300
            : 1000;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
