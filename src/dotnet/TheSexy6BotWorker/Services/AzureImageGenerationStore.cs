using System.Globalization;
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Extensions.Options;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Models;

namespace TheSexy6BotWorker.Services;

public sealed class AzureImageGenerationStore : IImageGenerationStore
{
    private const string DedupePartitionKey = "image-generation";
    private readonly ImageGenerationOptions _options;
    private readonly ILogger<AzureImageGenerationStore> _logger;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private TableClient? _quotaTable;
    private TableClient? _dedupeTable;
    private TableClient? _metadataTable;

    public AzureImageGenerationStore(
        IOptions<ImageGenerationOptions> options,
        ILogger<AzureImageGenerationStore> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImageGenerationResult?> TryGetStoredResultAsync(
        ulong sourceMessageId,
        CancellationToken cancellationToken = default)
    {
        var table = await GetDedupeTableAsync(cancellationToken).ConfigureAwait(false);
        var key = sourceMessageId.ToString(CultureInfo.InvariantCulture);

        try
        {
            var response = await table.GetEntityAsync<ImageGenerationRecordEntity>(
                DedupePartitionKey,
                key,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Value.Status is not ("completed" or "failed"))
            {
                return null;
            }

            return MapToResult(response.Value, isNewGeneration: false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> TryAcquireAsync(
        ImageGenerationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var table = await GetDedupeTableAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var entity = new ImageGenerationRecordEntity
        {
            PartitionKey = DedupePartitionKey,
            RowKey = context.SourceMessageId.ToString(CultureInfo.InvariantCulture),
            Status = "processing",
            SourceMessageId = context.SourceMessageId.ToString(CultureInfo.InvariantCulture),
            ChannelId = context.ChannelId.ToString(CultureInfo.InvariantCulture),
            UserId = context.UserId.ToString(CultureInfo.InvariantCulture),
            Origin = context.IsAuto ? "auto" : "manual",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        try
        {
            await table.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return false;
        }
    }

    public async Task MarkCompletedAsync(
        ImageGenerationResult result,
        ImageGenerationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var table = await GetDedupeTableAsync(cancellationToken).ConfigureAwait(false);
        var entity = BuildRecordEntity(result, context, "completed");
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        ImageGenerationResult result,
        ImageGenerationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var table = await GetDedupeTableAsync(cancellationToken).ConfigureAwait(false);
        var entity = BuildRecordEntity(result, context, "failed");
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    public async Task<QuotaReservationResult> TryReserveQuotaAsync(
        ImageGenerationExecutionContext context,
        string promptHash,
        CancellationToken cancellationToken = default)
    {
        var table = await GetQuotaTableAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var partitionKey = context.UserId.ToString(CultureInfo.InvariantCulture);
        var rowKey = now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var resetAt = GetNextUtcMidnight(now);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var existing = await TryGetQuotaEntityAsync(table, partitionKey, rowKey, cancellationToken).ConfigureAwait(false);
                if (existing is null)
                {
                    var created = new QuotaEntity
                    {
                        PartitionKey = partitionKey,
                        RowKey = rowKey,
                        Count = 1,
                        Limit = _options.DailyQuotaLimit,
                        PromptHash = promptHash,
                        ResetAtUtc = resetAt,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };

                    await table.AddEntityAsync(created, cancellationToken).ConfigureAwait(false);
                    return new QuotaReservationResult(true, partitionKey, rowKey, 0, 1, _options.DailyQuotaLimit, resetAt);
                }

                if (existing.Count >= _options.DailyQuotaLimit)
                {
                    return new QuotaReservationResult(
                        false,
                        partitionKey,
                        rowKey,
                        existing.Count,
                        existing.Count,
                        _options.DailyQuotaLimit,
                        existing.ResetAtUtc,
                        $"Daily image quota of {_options.DailyQuotaLimit} has been reached.");
                }

                existing.Count++;
                existing.PromptHash = promptHash;
                existing.Limit = _options.DailyQuotaLimit;
                existing.ResetAtUtc = resetAt;
                existing.UpdatedAtUtc = now;

                await table.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
                return new QuotaReservationResult(true, partitionKey, rowKey, existing.Count - 1, existing.Count, _options.DailyQuotaLimit, resetAt);
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                _logger.LogDebug("Quota update raced for user {UserId}; retrying attempt {Attempt}.", context.UserId, attempt);
            }
        }

        return new QuotaReservationResult(
            false,
            partitionKey,
            rowKey,
            0,
            0,
            _options.DailyQuotaLimit,
            resetAt,
            "Unable to reserve quota because the quota row was updated concurrently.");
    }

    public async Task ReleaseQuotaAsync(
        QuotaReservationResult reservation,
        CancellationToken cancellationToken = default)
    {
        if (!reservation.Success)
        {
            return;
        }

        var table = await GetQuotaTableAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var existing = await TryGetQuotaEntityAsync(table, reservation.PartitionKey, reservation.RowKey, cancellationToken).ConfigureAwait(false);
            if (existing is null || existing.Count <= 0)
            {
                return;
            }

            existing.Count--;
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await table.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Nothing to release.
        }
    }

    public async Task TryStoreMetadataAsync(
        ImageGenerationResult result,
        ImageGenerationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var table = await GetMetadataTableAsync(cancellationToken).ConfigureAwait(false);
        var entity = BuildRecordEntity(result, context, result.Success ? "completed" : "failed");
        entity.PartitionKey = result.CreatedAtUtc.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        entity.RowKey = context.SourceMessageId.ToString(CultureInfo.InvariantCulture);
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    private static ImageGenerationResult MapToResult(ImageGenerationRecordEntity entity, bool isNewGeneration)
    {
        var requestedModel = ParseModelChoice(entity.RequestedModel);
        var resolvedModel = ParseModelChoice(entity.ResolvedModel);
        GeneratedImageHistoryMarker? marker = null;
        if (!string.IsNullOrWhiteSpace(entity.MarkerJson)
            && GeneratedImageHistoryMarker.TryParse(entity.MarkerJson, out var parsedMarker))
        {
            marker = parsedMarker;
        }

        return new ImageGenerationResult
        {
            Success = entity.Status == "completed",
            IsNewGeneration = isNewGeneration,
            UsedFallback = entity.UsedFallback,
            Prompt = entity.Prompt,
            PromptHash = entity.PromptHash,
            RequestedModel = requestedModel,
            ResolvedModel = resolvedModel,
            ModelId = entity.ModelId,
            BlobName = entity.BlobName,
            BlobUrl = entity.BlobUrl,
            ContentType = string.IsNullOrWhiteSpace(entity.ContentType) ? "image/png" : entity.ContentType,
            DisplayMessage = entity.DisplayMessage,
            ErrorMessage = entity.ErrorMessage,
            CreatedAtUtc = entity.CreatedAtUtc,
            HistoryMarker = marker
        };
    }

    private static ImageGenerationRecordEntity BuildRecordEntity(
        ImageGenerationResult result,
        ImageGenerationExecutionContext context,
        string status)
    {
        return new ImageGenerationRecordEntity
        {
            PartitionKey = DedupePartitionKey,
            RowKey = context.SourceMessageId.ToString(CultureInfo.InvariantCulture),
            Status = status,
            SourceMessageId = context.SourceMessageId.ToString(CultureInfo.InvariantCulture),
            ChannelId = context.ChannelId.ToString(CultureInfo.InvariantCulture),
            UserId = context.UserId.ToString(CultureInfo.InvariantCulture),
            Origin = context.IsAuto ? "auto" : "manual",
            Prompt = result.Prompt,
            PromptHash = result.PromptHash,
            RequestedModel = result.RequestedModel.ToAlias(),
            ResolvedModel = result.ResolvedModel.ToAlias(),
            ModelId = result.ModelId,
            UsedFallback = result.UsedFallback,
            BlobName = result.BlobName,
            BlobUrl = result.BlobUrl,
            ContentType = result.ContentType,
            DisplayMessage = result.DisplayMessage,
            ErrorMessage = result.ErrorMessage,
            MarkerJson = result.HistoryMarker?.ToJson(),
            CreatedAtUtc = result.CreatedAtUtc,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static async Task<QuotaEntity?> TryGetQuotaEntityAsync(
        TableClient table,
        string partitionKey,
        string rowKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await table.GetEntityAsync<QuotaEntity>(partitionKey, rowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static ImageGenerationModelChoice ParseModelChoice(string? value)
    {
        return ImageGenerationModelChoiceExtensions.TryParseAlias(value, out var parsed)
            ? parsed
            : ImageGenerationModelChoice.Flux;
    }

    private static DateTimeOffset GetNextUtcMidnight(DateTimeOffset now)
    {
        var nextDay = now.UtcDateTime.Date.AddDays(1);
        return new DateTimeOffset(DateTime.SpecifyKind(nextDay, DateTimeKind.Utc));
    }

    private async Task<TableClient> GetQuotaTableAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return _quotaTable!;
    }

    private async Task<TableClient> GetDedupeTableAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return _dedupeTable!;
    }

    private async Task<TableClient> GetMetadataTableAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return _metadataTable!;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_quotaTable is not null && _dedupeTable is not null && _metadataTable is not null)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_quotaTable is not null && _dedupeTable is not null && _metadataTable is not null)
            {
                return;
            }

            TableServiceClient serviceClient;
            if (!string.IsNullOrWhiteSpace(_options.StorageConnectionString))
            {
                serviceClient = new TableServiceClient(_options.StorageConnectionString);
            }
            else
            {
                var accountName = _options.StorageAccountName;
                if (string.IsNullOrWhiteSpace(accountName))
                {
                    throw new InvalidOperationException(
                        "Set ImageGeneration:StorageConnectionString for Azurite/local runs or ImageGeneration:StorageAccountName for Azure.");
                }

                var serviceUri = new Uri($"https://{accountName}.table.core.windows.net", UriKind.Absolute);
                serviceClient = new TableServiceClient(serviceUri, new DefaultAzureCredential());
            }

            _quotaTable = serviceClient.GetTableClient(_options.QuotaTableName);
            _dedupeTable = serviceClient.GetTableClient(_options.DedupeTableName);
            _metadataTable = serviceClient.GetTableClient(_options.MetadataTableName);

            await _quotaTable.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            await _dedupeTable.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            await _metadataTable.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private sealed class ImageGenerationRecordEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;

        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        public string Status { get; set; } = string.Empty;

        public string SourceMessageId { get; set; } = string.Empty;

        public string ChannelId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string Origin { get; set; } = string.Empty;

        public string Prompt { get; set; } = string.Empty;

        public string PromptHash { get; set; } = string.Empty;

        public string RequestedModel { get; set; } = string.Empty;

        public string ResolvedModel { get; set; } = string.Empty;

        public string ModelId { get; set; } = string.Empty;

        public bool UsedFallback { get; set; }

        public string BlobName { get; set; } = string.Empty;

        public string BlobUrl { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public string DisplayMessage { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public string? MarkerJson { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; }

        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class QuotaEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;

        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        public int Count { get; set; }

        public int Limit { get; set; }

        public string PromptHash { get; set; } = string.Empty;

        public DateTimeOffset ResetAtUtc { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; }

        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
