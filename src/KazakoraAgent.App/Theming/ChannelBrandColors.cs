using System.Windows.Media;
using KazakoraAgent.Core.Models;

namespace KazakoraAgent.App.Theming;

/// <summary>
/// Cor de identidade de cada marketplace — usada como faixa de destaque nos
/// cards de canal, não como cor de status (essa é StatusSuccess/Warning/
/// Error/Processing, sempre a mesma em Themes/BrandColors.xaml). Valores
/// aproximam a identidade visual pública de cada marca; ajustar aqui se o
/// usuário quiser tons diferentes, é o único lugar que precisa mudar.
/// </summary>
public static class ChannelBrandColors
{
    public static readonly Color Store = (Color) ColorConverter.ConvertFromString("#00E676")!;

    public static readonly Color MercadoLivre = (Color) ColorConverter.ConvertFromString("#FFE600")!;

    public static readonly Color Shopee = (Color) ColorConverter.ConvertFromString("#EE4D2D")!;

    public static readonly Color TikTokShop = (Color) ColorConverter.ConvertFromString("#7B61FF")!;

    public static Color For(string channel) => channel switch
    {
        MarketplaceChannel.Store => Store,
        MarketplaceChannel.MercadoLivre => MercadoLivre,
        MarketplaceChannel.Shopee => Shopee,
        MarketplaceChannel.TikTokShop => TikTokShop,
        _ => Colors.Gray,
    };

    public static Brush BrushFor(string channel) => new SolidColorBrush(For(channel));

    public static string DisplayNameFor(string channel) => channel switch
    {
        MarketplaceChannel.Store => "Site Próprio",
        MarketplaceChannel.MercadoLivre => "Mercado Livre",
        MarketplaceChannel.Shopee => "Shopee",
        MarketplaceChannel.TikTokShop => "TikTok Shop",
        _ => channel,
    };
}
