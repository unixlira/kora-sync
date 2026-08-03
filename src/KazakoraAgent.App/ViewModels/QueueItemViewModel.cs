using System.Windows;
using System.Windows.Media;
using KazakoraAgent.App.Theming;
using KazakoraAgent.Core.Queue;

namespace KazakoraAgent.App.ViewModels;

public sealed class QueueItemViewModel
{
    public required long ServerJobId { get; init; }

    public required long OrderId { get; init; }

    public string? Channel { get; init; }

    public string? ChannelDisplayName => Channel is null ? null : ChannelBrandColors.DisplayNameFor(Channel);

    /// Usado como faixa de destaque nas linhas da listagem de detalhe (mesma
    /// cor de marca dos cards de canal), pra reforçar visualmente de qual
    /// marketplace veio o pedido.
    public Brush ChannelAccentBrush => Channel is null ? System.Windows.Media.Brushes.Gray : ChannelBrandColors.BrushFor(Channel);

    public string? ShippingType { get; init; }

    public required QueuedJobStatus Status { get; init; }

    public required string StatusLabel { get; init; }

    public required Brush StatusBrush { get; init; }

    public required int AttemptCount { get; init; }

    public string? LastError { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public static QueueItemViewModel FromDomain(QueuedJob job)
    {
        var (label, brush) = Describe(job.Status);

        var timestamp = job.Status switch
        {
            QueuedJobStatus.Printed => job.PrintedAt ?? job.EnqueuedAt,
            QueuedJobStatus.WaitingRetry or QueuedJobStatus.FailedPermanently => job.NextAttemptAt,
            _ => job.EnqueuedAt,
        };

        return new QueueItemViewModel
        {
            ServerJobId = job.ServerJobId,
            OrderId = job.OrderId,
            Channel = job.Channel,
            ShippingType = job.ShippingType,
            Status = job.Status,
            StatusLabel = label,
            StatusBrush = brush,
            AttemptCount = job.AttemptCount,
            LastError = job.LastError,
            Timestamp = timestamp,
        };
    }

    private static (string Label, Brush Brush) Describe(QueuedJobStatus status)
    {
        var resources = Application.Current.Resources;

        return status switch
        {
            QueuedJobStatus.Queued => ("Aguardando", (Brush) resources["StatusProcessingBrush"]),
            QueuedJobStatus.Processing => ("Processando", (Brush) resources["StatusProcessingBrush"]),
            QueuedJobStatus.WaitingRetry => ("Retentativa", (Brush) resources["StatusWarningBrush"]),
            QueuedJobStatus.Printed => ("Impresso", (Brush) resources["StatusSuccessBrush"]),
            QueuedJobStatus.FailedPermanently => ("Falhou", (Brush) resources["StatusErrorBrush"]),
            _ => (status.ToString(), (Brush) resources["StatusProcessingBrush"]),
        };
    }
}
