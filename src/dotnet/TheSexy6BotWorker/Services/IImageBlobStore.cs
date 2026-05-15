namespace TheSexy6BotWorker.Services;

public interface IImageBlobStore
{
    Task<ImageBlobUploadResult> UploadAsync(
        byte[] imageBytes,
        string contentType,
        string blobName,
        CancellationToken cancellationToken = default);
}

public sealed record ImageBlobUploadResult(
    string BlobName,
    string BlobUrl,
    string ContentType);
