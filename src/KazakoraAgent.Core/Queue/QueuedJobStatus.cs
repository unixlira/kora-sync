namespace KazakoraAgent.Core.Queue;

public enum QueuedJobStatus
{
    /// Aguardando a próxima tentativa (inclui a primeira).
    Queued,

    /// Sendo processado agora mesmo (baixando etiqueta + enviando pra impressora).
    Processing,

    /// Uma tentativa falhou e ainda restam tentativas — aguardando o backoff.
    WaitingRetry,

    Printed,

    /// Esgotou as tentativas — falha permanente, precisa de alerta visual/notificação.
    FailedPermanently,
}
