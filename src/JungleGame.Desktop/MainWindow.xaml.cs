using System.Windows;
using JungleGame.Desktop.ViewModels;

namespace JungleGame.Desktop;

/// <summary>
/// Main window for the Jungle board game application.
/// Sets up the MVVM DataContext and window-level event handling.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        BoardControl.ViewModel = _viewModel; // Direct assignment as fallback

        Loaded += (s, e) =>
        {
            _viewModel.StartNewGame();
        };
    }
}
