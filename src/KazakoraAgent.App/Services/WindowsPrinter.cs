using System.Diagnostics;
using System.IO;
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

    public async Task PrintAsync(byte[] pdfBytes, string printerName, CancellationToken ct = default)
    {
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
}
