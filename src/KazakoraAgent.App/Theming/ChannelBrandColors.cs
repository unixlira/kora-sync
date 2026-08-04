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
    // Verde correto da marca/logo (pedido explícito 2026-08-04) — mantido
    // sincronizado com BrandPrimaryColor em Themes/BrandColors.xaml.
    public static readonly Color Store = (Color) ColorConverter.ConvertFromString("#04D7B6")!;

    public static readonly Color MercadoLivre = (Color) ColorConverter.ConvertFromString("#FFE600")!;

    public static readonly Color Shopee = (Color) ColorConverter.ConvertFromString("#EE4D2D")!;

    public static readonly Color TikTokShop = (Color) ColorConverter.ConvertFromString("#7B61FF")!;

    public static readonly Color Amazon = (Color) ColorConverter.ConvertFromString("#FF9900")!;

    /// Marca da Shein é minimalista preto/branco — usa um cinza-escuro em
    /// vez de preto puro pra continuar visível como faixa/bolha tingida
    /// (preto puro com opacidade reduzida vira quase invisível nos dois
    /// temas).
    public static readonly Color Shein = (Color) ColorConverter.ConvertFromString("#3D3D3D")!;

    public static Color For(string channel) => channel switch
    {
        MarketplaceChannel.Store => Store,
        MarketplaceChannel.MercadoLivre => MercadoLivre,
        MarketplaceChannel.Shopee => Shopee,
        MarketplaceChannel.TikTokShop => TikTokShop,
        MarketplaceChannel.Amazon => Amazon,
        MarketplaceChannel.Shein => Shein,
        _ => Colors.Gray,
    };

    public static Brush BrushFor(string channel) => new SolidColorBrush(For(channel));

    public static string DisplayNameFor(string channel) => channel switch
    {
        MarketplaceChannel.Store => "KazaKora",
        MarketplaceChannel.MercadoLivre => "Mercado Livre",
        MarketplaceChannel.Shopee => "Shopee",
        MarketplaceChannel.TikTokShop => "TikTok Shop",
        MarketplaceChannel.Amazon => "Amazon",
        MarketplaceChannel.Shein => "Shein",
        _ => channel,
    };

    /// Nome curto pra contextos apertados (badges do card "Pedidos por
    /// canal") — só Mercado Livre e TikTok Shop têm abreviação real
    /// (pedido explícito 2026-08-04: "MeLi"); os demais já são curtos o
    /// bastante no nome de exibição normal.
    public static string ShortDisplayNameFor(string channel) => channel switch
    {
        MarketplaceChannel.MercadoLivre => "MeLi",
        MarketplaceChannel.TikTokShop => "TikTok",
        _ => DisplayNameFor(channel),
    };
}
