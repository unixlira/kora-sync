using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KazakoraAgent.App.ViewModels;

public partial class MetricCardViewModel : ObservableObject
{
    public required string Label { get; init; }

    public required Brush NumberBrush { get; init; }

    /// Cor do ícone e da bolha (badge) atrás dele — mesma cor, a bolha só
    /// usa opacidade reduzida (ver MetricCardTemplate).
    public required Brush AccentBrush { get; init; }

    /// Desenho em linhas retas só (sem arco) — mini-linguagem de path do
    /// WPF, viewport 24x24. Evita curvas/arcos de propósito: a sintaxe de
    /// arco (`A raio,raio ...`) nunca foi testada de verdade neste app, e
    /// um path mal formado só falha em runtime (Geometry.Parse), não em
    /// build — preferi ficar num subconjunto simples e confiável.
    public required Geometry Icon { get; init; }

    [ObservableProperty]
    private string _value = "--";
}
