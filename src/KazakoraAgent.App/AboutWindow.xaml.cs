using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace KazakoraAgent.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void DevLiraLink_Click(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://devlira.com.br") { UseShellExecute = true });
    }
}
