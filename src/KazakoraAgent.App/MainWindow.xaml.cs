using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using KazakoraAgent.App.Theming;
using KazakoraAgent.App.ViewModels;

namespace KazakoraAgent.App;

public partial class MainWindow : Window
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private WindowState _windowStateBeforeFullScreen;
    private bool _isFullScreen;
    private readonly DispatcherTimer _clockTimer;

    public MainWindow()
    {
        InitializeComponent();
        UpdateThemeIcon();

        // Relógio do cabeçalho — puramente local (sem chamada de rede),
        // por isso é um timer próprio aqui e não faz parte do
        // DashboardPoller (que só existe pra falar com a API).
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();

        // Não chama UpdateClock() direto aqui: DataContext (setado via
        // object initializer em App.xaml.cs, `new MainWindow { DataContext
        // = ... }`) só existe DEPOIS que o construtor termina — chamar
        // agora sempre encontraria null. Loaded já garante DataContext
        // presente, evitando 1s em branco até o primeiro tick do timer.
        Loaded += (_, _) => UpdateClock();
    }

    /// "terça-feira, 04/08/2026 15:42:07" — dddd sai em minúscula por
    /// padrão do .NET mesmo com CultureInfo pt-BR; capitaliza a primeira
    /// letra pra ficar como se escreve normalmente em português.
    private void UpdateClock()
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var now = DateTime.Now;
        var weekday = now.ToString("dddd", PtBr);
        weekday = char.ToUpper(weekday[0], PtBr) + weekday[1..];

        viewModel.CurrentDateTimeText = $"{weekday}, {now:dd/MM/yyyy HH:mm:ss}";
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
