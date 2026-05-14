using TheSexy6BotWorker.Models;

namespace TheSexy6BotWorker.Services;

public interface IImageGenerationStore
{
    Task<ImageGenerationResult?> TryGetStoredResultAsync(
        ulong sourceMessageId,
        CancellationToken cancellationToken = default);

    Task<bool> TryAcquireAsync(
        ImageGenerationExecutionContext context,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        ImageGenerationResult result,
        ImageGenerationExecutionContext context,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        ImageGenerationResult result,
        ImageGenerationExecutionContext context,
        CancellationToken cancellationToken = default);

    Task<QuotaReservationResult> TryReserveQuotaAsync(
        ImageGenerationExecutionContext context,
        string promptHash,
        CancellationToken cancellationToken = default);

    Task ReleaseQuotaAsync(
        QuotaReservationResult reservation,
        CancellationToken cancellationToken = default);

    Task TryStoreMetadataAsync(
        ImageGenerationResult result,
        ImageGenerationExecutionContext context,
        CancellationToken cancellationToken = default);
}

public sealed record QuotaReservationResult(
    bool Success,
    string PartitionKey,
    string RowKey,
    int PreviousCount,
    int CurrentCount,
    int Limit,
    DateTimeOffset ResetAtUtc,
    string? ErrorMessage = null);
