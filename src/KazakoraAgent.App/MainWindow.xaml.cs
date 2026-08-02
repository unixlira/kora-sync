using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using KazakoraAgent.App.Theming;

namespace KazakoraAgent.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        UpdateThemeIcon();
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
