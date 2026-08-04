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
        else if (e.Key == Key.Escape && _isFullScreen)
        {
            // Esc só sai do fullscreen — nunca entra (diferente do F11, que
            // alterna os dois sentidos).
            ToggleFullScreen();
        }
    }

    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            // Fora do fullscreen a janela continua fixa (sem redimensionar
            // manualmente) — só o F11 muda o tamanho, pedido explícito
            // 2026-08-04. Ver também Window.ResizeMode no XAML (estado
            // inicial, antes de qualquer toggle).
            ResizeMode = ResizeMode.NoResize;
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
