namespace KazakoraAgent.Core.Models;

/// <summary>Espelha as constantes Order::ORIGIN_* do Laravel — mesmos valores literais.</summary>
public static class MarketplaceChannel
{
    public const string Store = "loja";

    public const string MercadoLivre = "mercado_livre";

    public const string Shopee = "shopee";

    public const string TikTokShop = "tiktok_shop";

    public static readonly IReadOnlyList<string> All = [Store, MercadoLivre, Shopee, TikTokShop];
}
