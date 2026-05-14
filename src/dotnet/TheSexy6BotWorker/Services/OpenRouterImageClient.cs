using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Models;

namespace TheSexy6BotWorker.Services;

public sealed class OpenRouterImageClient
{
    private static readonly HashSet<string> AllowedAspectRatios = new(StringComparer.OrdinalIgnoreCase)
    {
        "1:1",
        "2:3",
        "3:2",
        "3:4",
        "4:3",
        "4:5",
        "5:4",
        "9:16",
        "16:9",
        "21:9"
    };

    private static readonly HashSet<string> AllowedImageSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        "0.5K",
        "1K",
        "2K",
        "4K"
    };

    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ImageGenerationOptions _options;
    private readonly ILogger<OpenRouterImageClient> _logger;

    public OpenRouterImageClient(
        HttpClient httpClient,
        IOptions<ImageGenerationOptions> options,
        ILogger<OpenRouterImageClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = NormalizeEndpoint(_options.OpenRouterEndpoint);
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(10, _options.OpenRouterTimeoutSeconds));
    }

    public async Task<OpenRouterImageResult> GenerateAsync(
        string prompt,
        string modelId,
        string? aspectRatio,
        string? imageSize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ImageGenerationProviderException("Image prompt is required.", retryable: false);
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ImageGenerationProviderException("OpenRouter model id is required.", retryable: false);
        }

        var apiKey = _options.OpenRouterApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ImageGenerationProviderException("OpenRouter API key is not configured.", retryable: false);
        }

        var normalizedAspectRatio = NormalizeAspectRatio(aspectRatio, _options.AspectRatio);
        var normalizedImageSize = NormalizeImageSize(imageSize, _options.ImageSize);

        var payload = new OpenRouterChatCompletionRequest
        {
            Model = modelId,
            Messages = [new OpenRouterMessage("user", prompt)],
            Modalities = ["image"],
            Stream = false,
            ImageConfig = new OpenRouterImageConfig
            {
                AspectRatio = normalizedAspectRatio,
                ImageSize = normalizedImageSize
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload, options: RequestJsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var retryable = IsRetryableStatusCode(response.StatusCode);
            throw new ImageGenerationProviderException(
                $"OpenRouter request failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                retryable,
                (int)response.StatusCode,
                rawResponse);
        }

        var parsed = JsonSerializer.Deserialize<OpenRouterChatCompletionResponse>(rawResponse, ResponseJsonOptions);
        var imageUrl = parsed?.Choices?
            .FirstOrDefault()?
            .Message?
            .Images?
            .FirstOrDefault()?
            .ImageUrl?
            .Url;

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ImageGenerationProviderException(
                "OpenRouter returned a response without an image payload.",
                retryable: true,
                statusCode: null,
                rawResponse);
        }

        var decoded = await DecodeImageAsync(imageUrl, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "OpenRouter image response received from model {ModelId} using aspect ratio {AspectRatio} and size {ImageSize}.",
            modelId,
            normalizedAspectRatio,
            normalizedImageSize);

        return new OpenRouterImageResult(
            decoded.ImageBytes,
            decoded.ContentType,
            rawResponse,
            normalizedAspectRatio,
            normalizedImageSize);
    }

    private async Task<DecodedImage> DecodeImageAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = imageUrl.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
            if (separatorIndex < 0)
            {
                throw new ImageGenerationProviderException("OpenRouter returned an unsupported data URL.", retryable: true);
            }

            var metadata = imageUrl[..separatorIndex];
            var base64 = imageUrl[(separatorIndex + "base64,".Length)..];
            var dataUrlContentType = ParseContentType(metadata);

            try
            {
                return new DecodedImage(Convert.FromBase64String(base64), dataUrlContentType);
            }
            catch (FormatException ex)
            {
                throw new ImageGenerationProviderException("OpenRouter returned an invalid base64 image payload.", retryable: true, innerException: ex);
            }
        }

        using var response = await _httpClient.GetAsync(imageUrl, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ImageGenerationProviderException(
                $"OpenRouter image download failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                retryable: true,
                (int)response.StatusCode);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
        return new DecodedImage(bytes, contentType);
    }

    private static string NormalizeAspectRatio(string? value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return AllowedAspectRatios.Contains(candidate) ? candidate : fallback;
    }

    private static string NormalizeImageSize(string? value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return AllowedImageSizes.Contains(candidate) ? candidate : fallback;
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        var numeric = (int)statusCode;
        return numeric == 408 || numeric == 429 || numeric >= 500;
    }

    private static Uri NormalizeEndpoint(string endpoint)
    {
        var raw = string.IsNullOrWhiteSpace(endpoint)
            ? "https://openrouter.ai/api/v1/"
            : endpoint.Trim();

        if (!raw.EndsWith("/", StringComparison.Ordinal))
        {
            raw += "/";
        }

        return new Uri(raw, UriKind.Absolute);
    }

    private static string ParseContentType(string metadata)
    {
        var start = metadata.IndexOf("data:", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return "image/png";
        }

        var semicolon = metadata.IndexOf(';', start + 5);
        if (semicolon < 0)
        {
            return "image/png";
        }

        var contentType = metadata[(start + 5)..semicolon].Trim();
        return string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType;
    }

    private sealed record OpenRouterChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("messages")]
        public IReadOnlyList<OpenRouterMessage> Messages { get; init; } = [];

        [JsonPropertyName("modalities")]
        public IReadOnlyList<string> Modalities { get; init; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }

        [JsonPropertyName("image_config")]
        public OpenRouterImageConfig ImageConfig { get; init; } = new();
    }

    private sealed record OpenRouterMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OpenRouterImageConfig
    {
        [JsonPropertyName("aspect_ratio")]
        public string AspectRatio { get; init; } = "1:1";

        [JsonPropertyName("image_size")]
        public string ImageSize { get; init; } = "1K";
    }

    private sealed class OpenRouterChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenRouterChoice> Choices { get; set; } = [];
    }

    private sealed class OpenRouterChoice
    {
        [JsonPropertyName("message")]
        public OpenRouterMessageResponse? Message { get; set; }
    }

    private sealed class OpenRouterMessageResponse
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("images")]
        public List<OpenRouterImageResponse>? Images { get; set; }
    }

    private sealed class OpenRouterImageResponse
    {
        [JsonPropertyName("image_url")]
        public OpenRouterImageUrl? ImageUrl { get; set; }
    }

    private sealed class OpenRouterImageUrl
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    private sealed record DecodedImage(byte[] ImageBytes, string ContentType);
}

public sealed record OpenRouterImageResult(
    byte[] ImageBytes,
    string ContentType,
    string RawResponse,
    string ResolvedAspectRatio,
    string ResolvedImageSize);

public sealed class ImageGenerationProviderException : Exception
{
    public ImageGenerationProviderException(
        string message,
        bool retryable,
        int? statusCode = null,
        string? rawResponse = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Retryable = retryable;
        StatusCode = statusCode;
        RawResponse = rawResponse;
    }

    public bool Retryable { get; }

    public int? StatusCode { get; }

    public string? RawResponse { get; }
}
