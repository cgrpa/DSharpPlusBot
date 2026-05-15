using TheSexy6BotWorker.Models;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker.Tests.Models;

public class GeneratedImageHistoryMarkerTests
{
    [Fact]
    public void FromResult_RoundTripsStrictJsonMarker()
    {
        var createdAt = new DateTimeOffset(2026, 5, 14, 10, 30, 0, TimeSpan.Zero);
        var result = ImageGenerationResult.CreateSuccess(
            "a neon harbour",
            "abc123",
            ImageGenerationModelChoice.Flux,
            ImageGenerationModelChoice.Seedream,
            "bytedance-seed/seedream-4.5",
            "2026/05/14/42/seedream-1K-abc123.png",
            "https://images.example/seedream.png",
            "image/png",
            createdAt,
            "Generated an image.",
            usedFallback: true);
        var context = new ImageGenerationExecutionContext(42, 100, 200, 300, IsAuto: true);

        var marker = GeneratedImageHistoryMarker.FromResult(result, context);
        var json = marker.ToJson();

        Assert.True(GeneratedImageHistoryMarker.TryParse(json, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal("generated_image", parsed.Kind);
        Assert.Equal(GeneratedImageHistoryMarker.CurrentVersion, parsed.Version);
        Assert.Equal(42ul, parsed.SourceMessageId);
        Assert.Equal("auto", parsed.Origin);
        Assert.Equal("flux", parsed.RequestedModel);
        Assert.Equal("seedream", parsed.ResolvedModel);
        Assert.True(parsed.UsedFallback);
    }

    [Fact]
    public void TryParse_RejectsWrongKind()
    {
        var parsed = GeneratedImageHistoryMarker.TryParse("""{"kind":"not_image","version":1}""", out var marker);

        Assert.False(parsed);
        Assert.Null(marker);
    }
}
