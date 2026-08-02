using Microsoft.Win32;

namespace KazakoraAgent.App.Services;

/// <summary>
/// "Iniciar junto com o Windows" — chave de Run do usuário atual (não
/// precisa admin, não afeta outros usuários da máquina).
/// </summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KoraSync";

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);

        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            var exePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
            key.SetValue(ValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
