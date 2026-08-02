namespace KazakoraAgent.Core.Printing;

/// <summary>
/// Abstrai o envio real pro spooler do Windows (implementação real vive na
/// camada de app WPF, usando a API de impressão do Windows — igual o
/// pdf-to-printer fazia no agente Node) pra o motor de fila ser testável
/// sem uma impressora de verdade.
/// </summary>
public interface IPrinter
{
    Task PrintAsync(byte[] pdfBytes, string printerName, CancellationToken ct = default);
}
