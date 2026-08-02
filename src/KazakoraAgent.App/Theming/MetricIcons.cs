using System.Windows.Media;

namespace KazakoraAgent.App.Theming;

/// <summary>
/// Ícones dos cards de métrica — viewport 24x24, só linhas retas (M/L/Z),
/// sem arco. Ver comentário em MetricCardViewModel.Icon pro porquê.
/// </summary>
public static class MetricIcons
{
    public static readonly Geometry Revenue = Geometry.Parse(
        "M3,21 L7,21 L7,14 L3,14 Z M10,21 L14,21 L14,9 L10,9 Z M17,21 L21,21 L21,4 L17,4 Z");

    public static readonly Geometry Orders = Geometry.Parse(
        "M4,7 L20,7 L20,20 L4,20 Z");

    public static readonly Geometry Cancelled = Geometry.Parse(
        "M6,6 L18,18 M18,6 L6,18");

    public static readonly Geometry Refunds = Geometry.Parse(
        "M15,4 L7,12 L15,20 M7,12 L21,12");

    public static readonly Geometry Cart = Geometry.Parse(
        "M2,4 L4,4 L7,15 L18,15 L20,6 L5,6 M6,19 L10,19 L10,21 L6,21 Z M14,19 L18,19 L18,21 L14,21 Z");
}
