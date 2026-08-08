using System.Diagnostics;
using System.IO;
using System.Printing;
using KazakoraAgent.Core.Printing;

namespace KazakoraAgent.App.Services;

/// <summary>
/// Envia o PDF pro spooler do Windows via SumatraPDF (bundlado em
/// Tools/SumatraPDF.exe, mesma ferramenta que a lib `pdf-to-printer` usa
/// por baixo — abordagem já comprovada funcionando no agente Node
/// anterior). Foi trocado de um verbo "printto" do shell (que abria o
/// leitor de PDF padrão do Windows — nesta máquina, o Edge — pra pré-
/// visualização de impressão e ficava parado esperando alguém clicar,
/// travando a fila inteira; reproduzido e confirmado ao vivo 2026-08-02
/// antes dessa troca) pro `-print-to`/`-silent` do Sumatra, que imprime e
/// sai sozinho, sem abrir UI nenhuma.
/// </summary>
public sealed class WindowsPrinter : IPrinter
{
    private static readonly TimeSpan PrintTimeout = TimeSpan.FromSeconds(30);

    /// BUG REAL 2026-08-08 (achado depois de reclamação real do usuário —
    /// "diz que imprimiu mas não saiu papel", confirmado batendo no
    /// servidor: 65 de 65 print_jobs dos últimos 10 dias marcados
    /// "printed", zero falha registrada, estatisticamente impossível pra
    /// impressora térmica de uso real). Causa raiz: `-print-to -silent` do
    /// Sumatra retorna código 0 assim que o SPOOLER do Windows ACEITA o
    /// job — não espera o papel sair de verdade. Impressora offline, sem
    /// papel/etiqueta, pausada ou com erro de driver todos resultam no
    /// mesmo "sucesso" mentiroso: o exit code continua 0, QueueEngine
    /// marca Printed, reporta sucesso pro servidor, e ninguém nunca fica
    /// sabendo que nada saiu. Corrigido checando o status REAL da fila de
    /// impressão do Windows (System.Printing.PrintQueue) ANTES de mandar
    /// o job — se a impressora não está pronta, falha CEDO com uma
    /// mensagem específica, entrando no retry+notificação que o
    /// QueueEngine já tem pronto (WaitingRetry → FailedPermanently →
    /// JobFailedPermanently, que já dispara notificação do Windows) em vez
    /// de mentir sucesso. Não valida DEPOIS de imprimir (o status
    /// pós-envio de muitos drivers não é confiável — fica "Printing" por
    /// tempo indefinido mesmo já tendo saído), só antes, que é o sinal
    /// mais confiável disponível.
    private static readonly PrintQueueStatus BlockingStatus =
        PrintQueueStatus.Offline
        | PrintQueueStatus.Error
        | PrintQueueStatus.PaperOut
        | PrintQueueStatus.PaperJam
        | PrintQueueStatus.PaperProblem
        | PrintQueueStatus.DoorOpen
        | PrintQueueStatus.OutputBinFull
        | PrintQueueStatus.UserIntervention
        | PrintQueueStatus.NoToner
        | PrintQueueStatus.NotAvailable
        | PrintQueueStatus.Paused;

    public async Task PrintAsync(byte[] pdfBytes, string printerName, CancellationToken ct = default)
    {
        EnsurePrinterReady(printerName);

        var sumatraPath = Path.Combine(AppContext.BaseDirectory, "Tools", "SumatraPDF.exe");

        if (! File.Exists(sumatraPath))
        {
            throw new FileNotFoundException($"SumatraPDF.exe não encontrado em \"{sumatraPath}\" — reinstale o KoraSync.", sumatraPath);
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"korasync-label-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(tempPath, pdfBytes, ct);

        try
        {
            var startInfo = new ProcessStartInfo(sumatraPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            startInfo.ArgumentList.Add("-print-to");
            startInfo.ArgumentList.Add(printerName);
            startInfo.ArgumentList.Add("-silent");
            startInfo.ArgumentList.Add(tempPath);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Não foi possível iniciar o SumatraPDF.");

            // Timeout próprio além do ct externo — se o Sumatra travar por
            // qualquer motivo, isso não pode voltar a travar a fila inteira
            // como o bug anterior (o QueueEngine só processa 1 job por vez
            // por tick, um Print que nunca retorna trava tudo atrás dele).
            using var timeoutCts = new CancellationTokenSource(PrintTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                TryKill(process);

                throw new TimeoutException($"SumatraPDF não terminou em {PrintTimeout.TotalSeconds}s — processo encerrado à força.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"SumatraPDF terminou com código {process.ExitCode} (impressora \"{printerName}\" existe e está ligada?).");
            }
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
            }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    /// Lança se a impressora não existir OU estiver num estado que
    /// bloquearia a saída física do papel — ver comentário de
    /// BlockingStatus acima pro porquê disso existir.
    private static void EnsurePrinterReady(string printerName)
    {
        using var server = new LocalPrintServer();

        PrintQueue queue;

        try
        {
            queue = server.GetPrintQueue(printerName);
            queue.Refresh();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Impressora \"{printerName}\" não encontrada pelo Windows (renomeada? desinstalada?): {ex.Message}", ex);
        }

        using (queue)
        {
            var blocking = queue.QueueStatus & BlockingStatus;

            if (blocking != PrintQueueStatus.None || queue.IsOffline)
            {
                throw new InvalidOperationException($"Impressora \"{printerName}\" não está pronta: {DescribeStatus(blocking, queue.IsOffline)}. Confira papel/energia/cabo e a fila de impressão do Windows.");
            }
        }
    }

    private static string DescribeStatus(PrintQueueStatus status, bool isOffline)
    {
        var parts = new List<string>();

        if (isOffline || status.HasFlag(PrintQueueStatus.Offline)) parts.Add("offline");
        if (status.HasFlag(PrintQueueStatus.PaperOut)) parts.Add("sem papel/etiqueta");
        if (status.HasFlag(PrintQueueStatus.PaperJam)) parts.Add("papel emperrado");
        if (status.HasFlag(PrintQueueStatus.PaperProblem)) parts.Add("problema no papel");
        if (status.HasFlag(PrintQueueStatus.DoorOpen)) parts.Add("tampa aberta");
        if (status.HasFlag(PrintQueueStatus.OutputBinFull)) parts.Add("bandeja de saída cheia");
        if (status.HasFlag(PrintQueueStatus.UserIntervention)) parts.Add("precisa de intervenção manual");
        if (status.HasFlag(PrintQueueStatus.NoToner)) parts.Add("sem tinta/toner");
        if (status.HasFlag(PrintQueueStatus.NotAvailable)) parts.Add("indisponível");
        if (status.HasFlag(PrintQueueStatus.Paused)) parts.Add("pausada");
        if (status.HasFlag(PrintQueueStatus.Error)) parts.Add("erro genérico reportado pelo driver");

        return parts.Count > 0 ? string.Join(", ", parts) : "motivo não especificado pelo driver";
    }
}
