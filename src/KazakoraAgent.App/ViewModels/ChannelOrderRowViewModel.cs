namespace KazakoraAgent.App.ViewModels;

public sealed class ChannelOrderRowViewModel
{
    public required long Id { get; init; }

    public string? ExternalOrderId { get; init; }

    public required string CustomerName { get; init; }

    public required string ProductsSummary { get; init; }

    public required string StatusLabel { get; init; }

    public required decimal GrossAmount { get; init; }

    /// null quando o canal ainda não tem integração de taxa real (ex.: Shopee/TikTok hoje).
    public decimal? FeeAmount { get; init; }

    public decimal? NetAmount { get; init; }

    public string? ShippingMethod { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
