using System.Diagnostics;
using System.IO;
using KazakoraAgent.Core.Printing;

namespace KazakoraAgent.App.Services;

/// <summary>
/// Envia o PDF pro spooler do Windows via o verbo "printto" do shell —
/// delega pro visualizador de PDF padrão instalado (Edge/Acrobat/etc.)
/// imprimir na impressora indicada, sem UI visível. Essa é a peça mais
/// arriscada do app inteiro: só existe uma forma real de validar (imprimir
/// de verdade numa impressora real) e isso só é possível na sua máquina —
/// não deu pra testar nada disso aqui. Se "printto" não funcionar com o
/// leitor de PDF instalado, a alternativa testada e confirmada no agente
/// Node anterior era a lib `pdf-to-printer` (usa PDFtoPrinter.exe /
/// SumatraPDF por baixo) — pode valer a pena portar a mesma abordagem se
/// isso aqui não imprimir de primeira.
/// </summary>
public sealed class WindowsPrinter : IPrinter
{
    public async Task PrintAsync(byte[] pdfBytes, string printerName, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"korasync-label-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(tempPath, pdfBytes, ct);

        try
        {
            var startInfo = new ProcessStartInfo(tempPath)
            {
                Verb = "printto",
                Arguments = $"\"{printerName}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Não foi possível iniciar o processo de impressão (nenhum leitor de PDF associado a .pdf no Windows?).");

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Processo de impressão terminou com código {process.ExitCode}.");
            }
        }
        finally
        {
            // Best-effort — alguns leitores de PDF seguram o arquivo aberto
            // por um instante depois do processo "terminar" (ex.: passam
            // pro processo já em execução do Edge). Não falha a impressão
            // por causa disso.
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
            }
        }
    }
}
