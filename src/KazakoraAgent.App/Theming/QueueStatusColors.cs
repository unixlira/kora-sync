using System.Windows.Media;

namespace KazakoraAgent.App.Theming;

/// <summary>
/// Cor de cada status da fila de impressão — mesmos valores de
/// Themes/BrandColors.xaml (StatusWarning/Processing/Success/Error e seus
/// variantes "Soft"), mas fixas em C# em vez de resolvidas via
/// Application.Current.Resources. Segue o mesmo padrão de
/// ChannelBrandColors: os cards de canal usam esse tipo de brush fixa e
/// nunca tiveram problema de renderização, diferente dos cards de status
/// da fila (que buscavam as brushes "Soft" via resource lookup em tempo de
/// execução — indexador de ResourceDictionary retorna null silenciosamente
/// se a chave não existir no dicionário mesclado nesse momento, e foi isso
/// que deixou os cards sem cor/ícone num build real do usuário).
/// </summary>
public static class QueueStatusColors
{
    public static readonly QueueStatusColorSet Warning = Build("#FFB74D", "#26FFB74D", "#66FFB74D");

    public static readonly QueueStatusColorSet Processing = Build("#40C4FF", "#2640C4FF", "#6640C4FF");

    public static readonly QueueStatusColorSet Success = Build("#00E676", "#2600E676", "#6600E676");

    public static readonly QueueStatusColorSet Error = Build("#FF5252", "#26FF5252", "#66FF5252");

    private static QueueStatusColorSet Build(string accentHex, string softBackgroundHex, string softBorderHex) => new(
        Accent: new SolidColorBrush((Color) ColorConverter.ConvertFromString(accentHex)!),
        SoftBackground: new SolidColorBrush((Color) ColorConverter.ConvertFromString(softBackgroundHex)!),
        SoftBorder: new SolidColorBrush((Color) ColorConverter.ConvertFromString(softBorderHex)!));
}

public sealed record QueueStatusColorSet(Brush Accent, Brush SoftBackground, Brush SoftBorder);
