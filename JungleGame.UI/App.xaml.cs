using System.Windows;
using JungleGame.UI.Views;

namespace JungleGame.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var startDialog = new StartDialog();
        if (startDialog.ShowDialog() == true)
        {
            var mainWindow = new MainWindow(
                startDialog.HumanFirst,
                startDialog.AiVsAi);
            mainWindow.Show();
        }
        else
        {
            Shutdown();
        }
    }
}
