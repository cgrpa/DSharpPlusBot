using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using TheSexy6BotWorker.Configuration;

namespace TheSexy6BotWorker.Services;

public sealed class AzureImageBlobStore : IImageBlobStore
{
    private readonly ImageGenerationOptions _options;
    private readonly ILogger<AzureImageBlobStore> _logger;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private BlobContainerClient? _containerClient;

    public AzureImageBlobStore(
        IOptions<ImageGenerationOptions> options,
        ILogger<AzureImageBlobStore> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImageBlobUploadResult> UploadAsync(
        byte[] imageBytes,
        string contentType,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        if (imageBytes.Length == 0)
        {
            throw new ArgumentException("Image bytes must not be empty.", nameof(imageBytes));
        }

        var container = await GetContainerAsync(cancellationToken).ConfigureAwait(false);

        var blobClient = container.GetBlobClient(blobName);
        await using var stream = new MemoryStream(imageBytes, writable: false);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken).ConfigureAwait(false);
        await blobClient.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType
        }, cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Uploaded generated image blob {BlobName}.", blobName);

        return new ImageBlobUploadResult(
            blobName,
            blobClient.Uri.AbsoluteUri,
            string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType);
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerClient is not null)
        {
            return _containerClient;
        }

        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_containerClient is not null)
            {
                return _containerClient;
            }

            BlobServiceClient serviceClient;
            if (!string.IsNullOrWhiteSpace(_options.StorageConnectionString))
            {
                serviceClient = new BlobServiceClient(_options.StorageConnectionString);
            }
            else
            {
                var accountName = _options.StorageAccountName;
                if (string.IsNullOrWhiteSpace(accountName))
                {
                    throw new InvalidOperationException(
                        "Set ImageGeneration:StorageConnectionString for Azurite/local runs or ImageGeneration:StorageAccountName for Azure.");
                }

                serviceClient = new BlobServiceClient(
                    new Uri($"https://{accountName}.blob.core.windows.net", UriKind.Absolute),
                    new DefaultAzureCredential());
            }

            var container = serviceClient.GetBlobContainerClient(_options.BlobContainerName);
            await container.CreateIfNotExistsAsync(
                publicAccessType: PublicAccessType.Blob,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _containerClient = container;
            return container;
        }
        finally
        {
            _initializationGate.Release();
        }
    }
}
