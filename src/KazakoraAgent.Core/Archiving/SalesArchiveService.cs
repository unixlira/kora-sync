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

    private static readonly TimeZoneInfo BrazilTimeZone = ResolveBrazilTimeZone();

    private static TimeZoneInfo ResolveBrazilTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            // Ambiente sem a base de fusos IANA (raro) — não pode derrubar
            // o arquivamento por causa disso, cai pro fuso local do SO.
            return TimeZoneInfo.Local;
        }
    }

    /// <summary>
    /// Monta "raiz/Mês/NomeDoCanal/DD" e garante que existe — cria a
    /// árvore inteira (mês, canal e dia) se qualquer nível ainda não
    /// existir, sem erro se já existir.
    /// </summary>
    public static string EnsureFolder(string archiveRoot, string? channel, DateTimeOffset when)
    {
        // Bug real encontrado 2026-08-09: "when" chega em UTC (QueueEngine
        // usa TimeProvider.System.GetUtcNow()) — sem converter pro horário
        // de Brasília antes de ler Month/Day, qualquer job impresso entre
        // 21h e meia-noite (horário local) caía na pasta do dia seguinte
        // (e, na virada do mês, na pasta do mês seguinte inteiro), fazendo
        // a pasta do dia certo parecer que nunca foi criada.
        var local = TimeZoneInfo.ConvertTime(when, BrazilTimeZone);
        var month = MonthNames[local.Month - 1];
        var day = local.Day.ToString("D2");
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
    /// Salva o arquivo e devolve o caminho final gravado (pra log). Nome
    /// segue o padrão pedido 2026-08-09: código de rastreio quando existir;
    /// sem ele, cai pro id de venda do canal (saleId — orders.external_
    /// order_id, praticamente nunca nulo pra pedido de canal de verdade);
    /// só cai pro "pedido-{id interno}" antigo se nenhum dos dois existir
    /// (etiqueta manual sem pedido associado).
    /// </summary>
    public static string Save(string archiveRoot, string? channel, string? trackingCode, string? saleId, long? orderId, DateTimeOffset when, byte[] contents)
    {
        var folder = EnsureFolder(archiveRoot, channel, when);
        var extension = DetectExtension(contents);

        var baseName = !string.IsNullOrWhiteSpace(trackingCode)
            ? trackingCode
            : !string.IsNullOrWhiteSpace(saleId)
                ? saleId
                : $"pedido-{orderId?.ToString() ?? "desconhecido"}";

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(invalidChar, '_');
        }

        var path = Path.Combine(folder, $"{baseName}.{extension}");
        File.WriteAllBytes(path, contents);

        return path;
    }
}
