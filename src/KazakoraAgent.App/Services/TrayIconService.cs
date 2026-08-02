using System.Windows.Forms;

namespace KazakoraAgent.App.Services;

/// <summary>
/// Ícone da bandeja + menu (Abrir Dashboard, Pausar/Retomar Fila,
/// Configurações, Sair) e notificações via balloon tip. WPF não tem
/// NotifyIcon próprio — usa o do Windows Forms (UseWindowsForms=true no
/// .csproj), abordagem padrão pra isso em apps WPF.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleQueueItem;
    private bool _queuePaused;

    public event Action? OpenDashboardRequested;

    public event Action<bool>? QueuePauseToggled;

    public event Action? OpenSettingsRequested;

    public event Action? OpenAboutRequested;

    public event Action? ExitRequested;

    public TrayIconService()
    {
        _toggleQueueItem = new ToolStripMenuItem("Pausar Fila", null, (_, _) => ToggleQueue());

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir Dashboard", null, (_, _) => OpenDashboardRequested?.Invoke());
        menu.Items.Add(_toggleQueueItem);
        menu.Items.Add("Configurações", null, (_, _) => OpenSettingsRequested?.Invoke());
        menu.Items.Add("Sobre", null, (_, _) => OpenAboutRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            // Extrai o ícone do próprio .exe em execução (embutido via
            // ApplicationIcon no .csproj) em vez de abrir Assets\app.ico
            // como arquivo solto — esse arquivo é um recurso WPF embutido
            // no assembly, não existe fisicamente na pasta de publish
            // (causou um crash real na abertura: DirectoryNotFoundException).
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!,
            Text = "KoraSync",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _notifyIcon.DoubleClick += (_, _) => OpenDashboardRequested?.Invoke();
    }

    private void ToggleQueue()
    {
        _queuePaused = !_queuePaused;
        _toggleQueueItem.Text = _queuePaused ? "Retomar Fila" : "Pausar Fila";
        QueuePauseToggled?.Invoke(_queuePaused);
    }

    public void ShowError(string title, string message) =>
        _notifyIcon.ShowBalloonTip(8000, title, message, ToolTipIcon.Error);

    public void ShowInfo(string title, string message) =>
        _notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
