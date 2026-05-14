using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Helpers;
using TheSexy6BotWorker.Models;

namespace TheSexy6BotWorker.Services;

public sealed class ImageGenerationService
{
    private readonly OpenRouterImageClient _imageClient;
    private readonly IImageBlobStore _blobStore;
    private readonly IImageGenerationStore _store;
    private readonly ImageGenerationContextAccessor _contextAccessor;
    private readonly ImageGenerationOptions _options;
    private readonly ILogger<ImageGenerationService> _logger;

    public ImageGenerationService(
        OpenRouterImageClient imageClient,
        IImageBlobStore blobStore,
        IImageGenerationStore store,
        ImageGenerationContextAccessor contextAccessor,
        IOptions<ImageGenerationOptions> options,
        ILogger<ImageGenerationService> logger)
    {
        _imageClient = imageClient ?? throw new ArgumentNullException(nameof(imageClient));
        _blobStore = blobStore ?? throw new ArgumentNullException(nameof(blobStore));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImageGenerationResult> GenerateAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = _contextAccessor.Current;
        if (context is null)
        {
            var failure = CreateFailureResult(
                request,
                "Image generation context is unavailable.",
                isNewGeneration: true,
                requestedModel: request.RequestedModel ?? ImageGenerationModelChoice.Flux);
            _contextAccessor.StoreLastResult(failure);
            return failure;
        }

        var prompt = NormalizePrompt(request.Prompt);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            var failure = CreateFailureResult(
                request,
                "A non-empty prompt is required.",
                isNewGeneration: true,
                requestedModel: request.RequestedModel ?? ImageGenerationModelChoice.Flux);
            _contextAccessor.StoreLastResult(failure);
            return failure;
        }

        if (!_options.Enabled || (context.IsAuto ? !_options.AutoEnabled : !_options.ManualEnabled))
        {
            var failure = CreateFailureResult(
                request,
                "Image generation is disabled by configuration.",
                isNewGeneration: true,
                requestedModel: request.RequestedModel ?? ImageGenerationModelChoice.Flux);
            _contextAccessor.StoreLastResult(failure);
            return failure;
        }

        var requestedModel = request.RequestedModel ?? ImageGenerationModelChoice.Flux;
        var promptHash = ComputePromptHash(prompt);

        var storedResult = await _store.TryGetStoredResultAsync(context.SourceMessageId, cancellationToken).ConfigureAwait(false);
        if (storedResult is not null)
        {
            _contextAccessor.StoreLastResult(storedResult);
            return storedResult;
        }

        var acquired = await _store.TryAcquireAsync(context, cancellationToken).ConfigureAwait(false);
        if (!acquired)
        {
            var duplicateFailure = CreateFailureResult(
                request,
                "Image generation already handled for this message.",
                isNewGeneration: false,
                requestedModel);
            _contextAccessor.StoreLastResult(duplicateFailure);
            return duplicateFailure;
        }

        var quotaReservation = await _store.TryReserveQuotaAsync(context, promptHash, cancellationToken).ConfigureAwait(false);
        if (!quotaReservation.Success)
        {
            var quotaFailure = ImageGenerationResult.CreateFailure(
                prompt,
                promptHash,
                requestedModel,
                quotaReservation.ErrorMessage ?? "Unable to reserve image quota.",
                isNewGeneration: true,
                createdAtUtc: DateTimeOffset.UtcNow);

            try
            {
                await _store.MarkFailedAsync(quotaFailure, context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to mark quota failure for message {MessageId}.", context.SourceMessageId);
            }

            _contextAccessor.StoreLastResult(quotaFailure);
            return quotaFailure;
        }

        try
        {
            var candidateModels = BuildCandidateOrder(requestedModel);
            ImageGenerationResult? successfulResult = null;
            Exception? lastFailure = null;

            foreach (var candidate in candidateModels)
            {
                try
                {
                    successfulResult = await GenerateWithCandidateAsync(
                        prompt,
                        promptHash,
                        request,
                        context,
                        requestedModel,
                        candidate,
                        cancellationToken).ConfigureAwait(false);

                    break;
                }
                catch (ImageGenerationProviderException ex)
                {
                    lastFailure = ex;
                    _logger.LogWarning(
                        ex,
                        "OpenRouter candidate {Candidate} failed while generating image for message {MessageId}.",
                        candidate.ToAlias(),
                        context.SourceMessageId);
                }
                catch (Exception ex)
                {
                    lastFailure = ex;
                    _logger.LogWarning(
                        ex,
                        "Unexpected candidate failure while generating image for message {MessageId}.",
                        context.SourceMessageId);
                }
            }

            if (successfulResult is null)
            {
                var failureMessage = lastFailure?.Message ?? "Image generation failed.";
                var failure = ImageGenerationResult.CreateFailure(
                    prompt,
                    promptHash,
                    requestedModel,
                    failureMessage,
                    isNewGeneration: true,
                    createdAtUtc: DateTimeOffset.UtcNow);

                await _store.ReleaseQuotaAsync(quotaReservation, cancellationToken).ConfigureAwait(false);
                await _store.MarkFailedAsync(failure, context, cancellationToken).ConfigureAwait(false);
                _contextAccessor.StoreLastResult(failure);
                return failure;
            }

            await PersistCompletedResultAsync(successfulResult, context, cancellationToken).ConfigureAwait(false);
            _contextAccessor.StoreLastResult(successfulResult);
            return successfulResult;
        }
        catch (Exception ex)
        {
            try
            {
                await _store.ReleaseQuotaAsync(quotaReservation, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception releaseEx)
            {
                _logger.LogWarning(releaseEx, "Failed to release quota reservation after image generation failure.");
            }

            var failure = ImageGenerationResult.CreateFailure(
                prompt,
                promptHash,
                requestedModel,
                $"Image generation failed unexpectedly: {ex.Message}",
                isNewGeneration: true,
                createdAtUtc: DateTimeOffset.UtcNow);

            try
            {
                await _store.MarkFailedAsync(failure, context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception markFailureEx)
            {
                _logger.LogWarning(markFailureEx, "Unable to mark unexpected image generation failure for message {MessageId}.", context.SourceMessageId);
            }

            _contextAccessor.StoreLastResult(failure);
            return failure;
        }
    }

    private async Task<ImageGenerationResult> GenerateWithCandidateAsync(
        string prompt,
        string promptHash,
        ImageGenerationRequest request,
        ImageGenerationExecutionContext context,
        ImageGenerationModelChoice requestedModel,
        ImageGenerationModelChoice candidate,
        CancellationToken cancellationToken)
    {
        var modelId = candidate.ToModelId(_options);
        var imageSize = NormalizeImageSize(request.ImageSize ?? _options.ImageSize);
        var aspectRatio = NormalizeAspectRatio(request.AspectRatio ?? _options.AspectRatio);

        var providerResult = await _imageClient.GenerateAsync(
            prompt,
            modelId,
            aspectRatio,
            imageSize,
            cancellationToken).ConfigureAwait(false);

        if (providerResult.ImageBytes.LongLength > _options.MaxDecodedBytes)
        {
            throw new ImageGenerationProviderException(
                $"Generated image exceeded the maximum allowed size of {_options.MaxDecodedBytes} bytes.",
                retryable: candidate == ImageGenerationModelChoice.Flux);
        }

        var blobName = BuildBlobName(context, candidate, promptHash, providerResult.ResolvedImageSize);
        var upload = await _blobStore.UploadAsync(
            providerResult.ImageBytes,
            providerResult.ContentType,
            blobName,
            cancellationToken).ConfigureAwait(false);

        var usedFallback = candidate != requestedModel;
        var now = DateTimeOffset.UtcNow;

        var displayMessage = usedFallback
            ? $"Generated an image with `{candidate.ToAlias()}` after `{requestedModel.ToAlias()}` failed."
            : $"Generated an image with `{candidate.ToAlias()}`.";

        var result = ImageGenerationResult.CreateSuccess(
            prompt,
            promptHash,
            requestedModel,
            candidate,
            modelId,
            upload.BlobName,
            upload.BlobUrl,
            upload.ContentType,
            now,
            displayMessage,
            usedFallback,
            imageBytes: providerResult.ImageBytes);

        var marker = GeneratedImageHistoryMarker.FromResult(result, context);
        return result with { HistoryMarker = marker };
    }

    private async Task PersistCompletedResultAsync(
        ImageGenerationResult result,
        ImageGenerationExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await _store.MarkCompletedAsync(result, context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to mark image generation as completed for message {MessageId}; continuing with best-effort delivery.", context.SourceMessageId);
        }

        try
        {
            await _store.TryStoreMetadataAsync(result, context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to persist image metadata for message {MessageId}; retrying once.", context.SourceMessageId);

            try
            {
                await _store.TryStoreMetadataAsync(result, context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception retryEx)
            {
                _logger.LogWarning(retryEx, "Second metadata persistence attempt failed for message {MessageId}.", context.SourceMessageId);
            }
        }
    }

    private static string BuildBlobName(
        ImageGenerationExecutionContext context,
        ImageGenerationModelChoice candidate,
        string promptHash,
        string imageSize)
    {
        var timestamp = DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var safeSize = imageSize.Replace(':', '-');
        return $"{timestamp}/{context.SourceMessageId}/{candidate.ToAlias()}-{safeSize}-{promptHash[..Math.Min(12, promptHash.Length)]}-{suffix}.png";
    }

    private static List<ImageGenerationModelChoice> BuildCandidateOrder(ImageGenerationModelChoice requestedModel)
    {
        return requestedModel == ImageGenerationModelChoice.Flux
            ? [ImageGenerationModelChoice.Flux, ImageGenerationModelChoice.Seedream]
            : [ImageGenerationModelChoice.Seedream];
    }

    private static string ComputePromptHash(string prompt)
    {
        var bytes = Encoding.UTF8.GetBytes(prompt.Trim());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizePrompt(string prompt)
    {
        return prompt.Trim();
    }

    private static string NormalizeAspectRatio(string? aspectRatio)
    {
        var candidate = string.IsNullOrWhiteSpace(aspectRatio) ? "1:1" : aspectRatio.Trim();
        return candidate;
    }

    private static string NormalizeImageSize(string? imageSize)
    {
        var candidate = string.IsNullOrWhiteSpace(imageSize) ? "1K" : imageSize.Trim();
        return candidate;
    }

    private static ImageGenerationResult CreateFailureResult(
        ImageGenerationRequest request,
        string errorMessage,
        bool isNewGeneration,
        ImageGenerationModelChoice requestedModel)
    {
        var prompt = NormalizePrompt(request.Prompt);
        var promptHash = string.IsNullOrWhiteSpace(prompt) ? string.Empty : ComputePromptHash(prompt);
        return ImageGenerationResult.CreateFailure(
            prompt,
            promptHash,
            requestedModel,
            errorMessage,
            isNewGeneration,
            DateTimeOffset.UtcNow);
    }
}
