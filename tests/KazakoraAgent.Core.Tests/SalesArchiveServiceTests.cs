using KazakoraAgent.Core.Archiving;

namespace KazakoraAgent.Core.Tests;

public class SalesArchiveServiceTests
{
    [Fact]
    public void ensure_folder_uses_brazil_local_date_not_utc_for_a_late_night_print()
    {
        // 22h de 09/ago em Brasília (UTC-3) = 01h de 10/ago em UTC. Sem
        // converter pro fuso local, isso criava a pasta do dia 10 em vez
        // do dia 09 — exatamente o bug real relatado (pasta do dia não
        // aparecia dentro do mês vigente na hora de conferir).
        var lateNightUtc = new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero);
        var archiveRoot = Path.Combine(Path.GetTempPath(), $"korasync-archive-tz-test-{Guid.NewGuid():N}");

        try
        {
            var folder = SalesArchiveService.EnsureFolder(archiveRoot, "shopee", lateNightUtc);

            Assert.Equal(Path.Combine(archiveRoot, "Agosto", "Shopee", "09"), folder);
            Assert.True(Directory.Exists(folder));
        }
        finally
        {
            if (Directory.Exists(archiveRoot))
            {
                Directory.Delete(archiveRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ensure_folder_crossing_a_month_boundary_still_lands_on_the_correct_local_month()
    {
        // 23h do último dia de julho em Brasília = 02h de 1º de agosto em
        // UTC — sem a conversão, esse job caía inteiro na pasta de Agosto
        // em vez de Julho.
        var monthBoundaryUtc = new DateTimeOffset(2026, 8, 1, 2, 0, 0, TimeSpan.Zero);
        var archiveRoot = Path.Combine(Path.GetTempPath(), $"korasync-archive-tz-test-{Guid.NewGuid():N}");

        try
        {
            var folder = SalesArchiveService.EnsureFolder(archiveRoot, "mercado_livre", monthBoundaryUtc);

            Assert.Equal(Path.Combine(archiveRoot, "Julho", "Mercado Livre", "31"), folder);
        }
        finally
        {
            if (Directory.Exists(archiveRoot))
            {
                Directory.Delete(archiveRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void save_names_the_file_by_tracking_code_when_available()
    {
        var whenUtc = new DateTimeOffset(2026, 8, 9, 19, 0, 0, TimeSpan.Zero); // 16h em Brasília
        var archiveRoot = Path.Combine(Path.GetTempPath(), $"korasync-archive-tz-test-{Guid.NewGuid():N}");
        var pdfBytes = "%PDF-1.4 fake"u8.ToArray();

        try
        {
            var path = SalesArchiveService.Save(archiveRoot, "loja", trackingCode: "BR999", saleId: "VENDA-123", orderId: 42, whenUtc, pdfBytes);

            Assert.Equal(Path.Combine(archiveRoot, "Agosto", "Loja", "09", "BR999.pdf"), path);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(archiveRoot))
            {
                Directory.Delete(archiveRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void save_falls_back_to_the_channel_sale_id_when_tracking_code_is_missing()
    {
        // Pedido explícito 2026-08-09: rastreio ainda não existe pra esse
        // envio (comum na Shopee, atribuído de forma assíncrona), mas o id
        // de venda do canal (external_order_id) praticamente nunca falta —
        // é um nome bem melhor que o antigo "pedido-{id interno}".
        var whenUtc = new DateTimeOffset(2026, 8, 9, 19, 0, 0, TimeSpan.Zero);
        var archiveRoot = Path.Combine(Path.GetTempPath(), $"korasync-archive-tz-test-{Guid.NewGuid():N}");
        var zipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 };

        try
        {
            var path = SalesArchiveService.Save(archiveRoot, "shopee", trackingCode: null, saleId: "2608091234567", orderId: 42, whenUtc, zipBytes);

            Assert.Equal(Path.Combine(archiveRoot, "Agosto", "Shopee", "09", "2608091234567.zip"), path);
        }
        finally
        {
            if (Directory.Exists(archiveRoot))
            {
                Directory.Delete(archiveRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void save_falls_back_to_the_internal_order_id_when_neither_tracking_code_nor_sale_id_exist()
    {
        // Só acontece pra etiqueta manual sem pedido de canal associado.
        var whenUtc = new DateTimeOffset(2026, 8, 9, 19, 0, 0, TimeSpan.Zero);
        var archiveRoot = Path.Combine(Path.GetTempPath(), $"korasync-archive-tz-test-{Guid.NewGuid():N}");
        var pdfBytes = "%PDF-1.4 fake"u8.ToArray();

        try
        {
            var path = SalesArchiveService.Save(archiveRoot, "loja", trackingCode: null, saleId: null, orderId: 42, whenUtc, pdfBytes);

            Assert.Equal(Path.Combine(archiveRoot, "Agosto", "Loja", "09", "pedido-42.pdf"), path);
        }
        finally
        {
            if (Directory.Exists(archiveRoot))
            {
                Directory.Delete(archiveRoot, recursive: true);
            }
        }
    }
}
