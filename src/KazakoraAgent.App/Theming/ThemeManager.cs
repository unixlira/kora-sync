using System.Windows;

namespace KazakoraAgent.App.Theming;

public enum AppTheme
{
    Dark,
    Light,
}

/// <summary>
/// Troca de tema em runtime — o dicionário do tema é sempre o índice 1 em
/// Application.Resources.MergedDictionaries (0 = BrandColors, fixo; 1 =
/// Dark ou Light; 2 = Typography; 3 = Controls). Ver App.xaml pra ordem.
/// </summary>
public static class ThemeManager
{
    private const int ThemeDictionaryIndex = 1;

    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    public static void SetTheme(AppTheme theme)
    {
        var uri = theme == AppTheme.Dark
            ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
            : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries[ThemeDictionaryIndex] = new ResourceDictionary { Source = uri };

        Current = theme;
    }

    public static void Toggle() => SetTheme(Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
}
