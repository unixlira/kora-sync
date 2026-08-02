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
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
    }

    private void DevLiraLink_Click(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://devlira.com.br") { UseShellExecute = true });
    }
}
