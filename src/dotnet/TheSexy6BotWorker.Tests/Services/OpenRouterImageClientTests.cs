using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Models;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker.Tests.Services;

public class OpenRouterImageClientTests
{
    [Fact]
    public async Task GenerateAsync_SendsExpectedPayloadAndDecodesDataUrl()
    {
        var imageBytes = new byte[] { 1, 2, 3, 4 };
        var dataUrl = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
        var handler = new ScriptedHttpMessageHandler(
        [
            JsonResponse(HttpStatusCode.OK, $$"""
            {
              "choices": [
                {
                  "message": {
                    "images": [
                      {
                        "image_url": {
                          "url": "{{dataUrl}}"
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """)
        ]);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        var service = CreateClient(client);

        var result = await service.GenerateAsync("draw a lighthouse", "black-forest-labs/flux.2-klein-4b", "16:9", "2K");

        Assert.Equal(imageBytes, result.ImageBytes);
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal("16:9", result.ResolvedAspectRatio);
        Assert.Equal("2K", result.ResolvedImageSize);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/v1/chat/completions", request.Path);
        Assert.Equal("Bearer test-openrouter-key", request.Authorization);

        using var bodyDocument = JsonDocument.Parse(request.Body);
        var root = bodyDocument.RootElement;
        Assert.Equal("black-forest-labs/flux.2-klein-4b", root.GetProperty("model").GetString());
        Assert.Equal("draw a lighthouse", root.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal("image", root.GetProperty("modalities")[0].GetString());
        Assert.Equal("16:9", root.GetProperty("image_config").GetProperty("aspect_ratio").GetString());
        Assert.Equal("2K", root.GetProperty("image_config").GetProperty("image_size").GetString());
    }

    [Fact]
    public async Task ImageGenerationService_WhenFluxFails_FallsBackToSeedream()
    {
        var imageBytes = new byte[] { 9, 8, 7 };
        var dataUrl = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
        var handler = new ScriptedHttpMessageHandler(
        [
            JsonResponse(HttpStatusCode.InternalServerError, """{"error":"flux failed"}"""),
            JsonResponse(HttpStatusCode.OK, $$"""
            {
              "choices": [
                {
                  "message": {
                    "images": [
                      {
                        "image_url": {
                          "url": "{{dataUrl}}"
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """)
        ]);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        var contextAccessor = new ImageGenerationContextAccessor();
        var store = new InMemoryImageGenerationStore();
        var service = new ImageGenerationService(
            CreateClient(httpClient),
            store,
            contextAccessor,
            Options.Create(CreateOptions()),
            NullLogger<ImageGenerationService>.Instance);

        using var scope = contextAccessor.Push(new ImageGenerationExecutionContext(123, 456, 789, IsAuto: false));
        var result = await service.GenerateAsync(new ImageGenerationRequest
        {
            Prompt = "a glossy red train",
            RequestedModel = ImageGenerationModelChoice.Flux
        });

        Assert.True(result.Success);
        Assert.True(result.UsedFallback);
        Assert.Equal(ImageGenerationModelChoice.Flux, result.RequestedModel);
        Assert.Equal(ImageGenerationModelChoice.Seedream, result.ResolvedModel);
        Assert.Equal("bytedance-seed/seedream-4.5", result.ModelId);
        Assert.Equal(imageBytes, result.ImageBytes);
        Assert.Matches(@"^123-seedream-[a-f0-9]{10}-[a-f0-9]{6}\.png$", result.BlobName);
        Assert.Equal($"attachment://{result.BlobName}", result.BlobUrl);
        Assert.Single(store.CompletedResults);
        Assert.Single(store.MetadataResults);
        Assert.Equal(2, handler.Requests.Count);

        using var firstRequest = JsonDocument.Parse(handler.Requests[0].Body);
        using var secondRequest = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.Equal("black-forest-labs/flux.2-klein-4b", firstRequest.RootElement.GetProperty("model").GetString());
        Assert.Equal("bytedance-seed/seedream-4.5", secondRequest.RootElement.GetProperty("model").GetString());
    }

    private static OpenRouterImageClient CreateClient(HttpClient httpClient)
    {
        return new OpenRouterImageClient(
            httpClient,
            Options.Create(CreateOptions()),
            NullLogger<OpenRouterImageClient>.Instance);
    }

    private static ImageGenerationOptions CreateOptions()
    {
        return new ImageGenerationOptions
        {
            OpenRouterApiKey = "test-openrouter-key",
            OpenRouterEndpoint = "https://openrouter.ai/api/v1/",
            DefaultModelId = "black-forest-labs/flux.2-klein-4b",
            FallbackModelId = "bytedance-seed/seedream-4.5",
            StorageAccountName = "testimages",
            DailyQuotaLimit = 10,
            MaxDecodedBytes = 25 * 1024 * 1024,
            AspectRatio = "1:1",
            ImageSize = "1K"
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string payload)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload)
        };
    }

    private sealed class ScriptedHttpMessageHandler(IEnumerable<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.Method.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                body));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No scripted response is available.");
            }

            return _responses.Dequeue();
        }
    }

    private sealed record CapturedRequest(string Method, string Path, string Authorization, string Body);

    private sealed class InMemoryImageGenerationStore : IImageGenerationStore
    {
        public List<ImageGenerationResult> CompletedResults { get; } = [];

        public List<ImageGenerationResult> MetadataResults { get; } = [];

        public Task<ImageGenerationResult?> TryGetStoredResultAsync(
            ulong sourceMessageId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ImageGenerationResult?>(null);
        }

        public Task<bool> TryAcquireAsync(
            ImageGenerationExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task MarkCompletedAsync(
            ImageGenerationResult result,
            ImageGenerationExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            CompletedResults.Add(result);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            ImageGenerationResult result,
            ImageGenerationExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<QuotaReservationResult> TryReserveQuotaAsync(
            ImageGenerationExecutionContext context,
            string promptHash,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new QuotaReservationResult(
                true,
                context.UserId.ToString(),
                "20260514",
                0,
                1,
                10,
                new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero)));
        }

        public Task ReleaseQuotaAsync(
            QuotaReservationResult reservation,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task TryStoreMetadataAsync(
            ImageGenerationResult result,
            ImageGenerationExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            MetadataResults.Add(result);
            return Task.CompletedTask;
        }
    }
}
