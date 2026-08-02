using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using KazakoraAgent.App.Theming;

namespace KazakoraAgent.App;

public partial class MainWindow : Window
{
    private WindowState _windowStateBeforeFullScreen;
    private bool _isFullScreen;

    public MainWindow()
    {
        InitializeComponent();
        UpdateThemeIcon();
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
        }
    }

    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = _windowStateBeforeFullScreen;
        }
        else
        {
            _windowStateBeforeFullScreen = WindowState;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }

        _isFullScreen = !_isFullScreen;
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        UpdateThemeIcon();
    }

    /// Mostra o ícone do que vai acontecer ao clicar — sol enquanto está
    /// escuro (clique acende), lua enquanto está claro (clique apaga).
    private void UpdateThemeIcon()
    {
        var isDark = ThemeManager.Current == AppTheme.Dark;
        SunIcon.Visibility = isDark ? Visibility.Visible : Visibility.Collapsed;
        MoonIcon.Visibility = isDark ? Visibility.Collapsed : Visibility.Visible;
    }

    private void DevLiraLink_Click(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://devlira.com.br") { UseShellExecute = true });
    }
}
