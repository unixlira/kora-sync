namespace KazakoraAgent.Core.Archiving;

/// <summary>
/// Guarda uma cópia local do arquivo bruto de cada etiqueta impressa, numa
/// pasta organizada por Mês/Canal/Dia (pedido explícito 2026-08-06),
/// nomeada pelo código de rastreio real — ex:
/// "Vendas\Agosto\Shopee\06\BR123456789.zip". Best-effort: nunca deve
/// interromper a impressão em si (ver uso em QueueEngine), só fica sem
/// arquivo local se falhar.
/// </summary>
public static class SalesArchiveService
{
    private static readonly string[] MonthNames =
    {
        "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
        "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro",
    };

    /// <summary>
    /// Monta "raiz/Mês/NomeDoCanal/DD" e garante que existe — cria a
    /// árvore inteira (mês, canal e dia) se qualquer nível ainda não
    /// existir, sem erro se já existir.
    /// </summary>
    public static string EnsureFolder(string archiveRoot, string? channel, DateTimeOffset when)
    {
        var month = MonthNames[when.Month - 1];
        var day = when.Day.ToString("D2");
        var folder = Path.Combine(archiveRoot, month, ChannelDisplayName(channel), day);

        Directory.CreateDirectory(folder);

        return folder;
    }

    public static string ChannelDisplayName(string? channel) => channel switch
    {
        "shopee" => "Shopee",
        "mercado_livre" => "Mercado Livre",
        "tiktok_shop" => "TikTok Shop",
        "loja" or null => "Loja",
        _ => channel,
    };

    /// <summary>
    /// Detecta a extensão real pela assinatura dos bytes (não confia em
    /// content-type de canal — já visto vindo inútil, "application/force-
    /// download", ver LabelFetchService do lado do servidor).
    /// </summary>
    public static string DetectExtension(byte[] contents)
    {
        if (contents.Length >= 4 && contents[0] == 0x50 && contents[1] == 0x4B && contents[2] == 0x03 && contents[3] == 0x04)
        {
            return "zip";
        }

        if (contents.Length >= 4 && contents[0] == (byte) '%' && contents[1] == (byte) 'P' && contents[2] == (byte) 'D' && contents[3] == (byte) 'F')
        {
            return "pdf";
        }

        return "bin";
    }

    /// <summary>
    /// Salva o arquivo e devolve o caminho final gravado (pra log). O nome
    /// é o código de rastreio; sem ele (rastreio ainda não existia quando o
    /// envio foi confirmado), cai pra "pedido-{id}" em vez de travar o
    /// arquivamento por falta de nome bonito.
    /// </summary>
    public static string Save(string archiveRoot, string? channel, string? trackingCode, long? orderId, DateTimeOffset when, byte[] contents)
    {
        var folder = EnsureFolder(archiveRoot, channel, when);
        var extension = DetectExtension(contents);

        var baseName = string.IsNullOrWhiteSpace(trackingCode)
            ? $"pedido-{orderId?.ToString() ?? "desconhecido"}"
            : trackingCode;

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(invalidChar, '_');
        }

        var path = Path.Combine(folder, $"{baseName}.{extension}");
        File.WriteAllBytes(path, contents);

        return path;
    }
}
