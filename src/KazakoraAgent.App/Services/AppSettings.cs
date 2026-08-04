using System.IO;
using System.Text.Json;

namespace KazakoraAgent.App.Services;

/// <summary>
/// Persistida em %AppData%\KoraSync\settings.json — é o que a tela de
/// Configurações (URL da API, token, impressora, intervalos de polling,
/// notificações, iniciar com o Windows) lê/grava.
/// </summary>
public sealed class AppSettings
{
    public string ApiBaseUrl { get; set; } = "https://kazakora.devlira.com.br/api/print-agent/";

    public string ApiToken { get; set; } = "";

    public string AgentId { get; set; } = Environment.MachineName;

    public string PrinterName { get; set; } = "";

    public int QueuePollSeconds { get; set; } = 1;

    // Não lido em App.xaml.cs hoje (o intervalo do painel está fixo em 2s
    // direto lá, ver comentário lá) — sem tela de configuração pra esse
    // campo, um settings.json antigo salvo com 5 nunca seria sobrescrito
    // só por mudar o default aqui. Mantido no modelo por enquanto, mas não
    // é mais a fonte de verdade real.
    public int DashboardPollSeconds { get; set; } = 2;

    public bool NotificationsEnabled { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public string Theme { get; set; } = "Dark";

    /// Jobs impressos/com falha permanente há mais que isso são apagados do
    /// banco local automaticamente — o app não deve acumular dado pra
    /// sempre numa máquina de baixo armazenamento (mini PC dedicado).
    public int QueueRetentionDays { get; set; } = 30;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KoraSync", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // Config corrompida/ilegível — segue com defaults em vez de travar a inicialização.
        }

        return new AppSettings();
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
