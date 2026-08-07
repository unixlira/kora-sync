namespace KazakoraAgent.Core;

/// <summary>
/// Log em arquivo texto simples, sem dependência nova (Serilog etc) — só
/// pra ter um jeito confiável de ver o que aconteceu de verdade num job de
/// impressão sem depender de notificação do Windows (que pode passar
/// despercebida) ou de uma tela que já foi removida do layout (2026-08-06).
/// Fica em %APPDATA%/KoraSync/logs/app.log, mesma pasta base do
/// queue.db já usado pelo SqliteJobStore.
/// </summary>
public static class AppLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KoraSync", "logs", "app.log");

    private static readonly object Lock = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level}: {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line);
            }
        }
        catch
        {
            // Log é best-effort — uma falha ao escrever o próprio log não
            // pode derrubar o processamento real da fila.
        }
    }
}
