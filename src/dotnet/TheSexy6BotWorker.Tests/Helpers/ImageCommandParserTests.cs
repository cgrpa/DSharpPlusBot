using TheSexy6BotWorker.Helpers;
using TheSexy6BotWorker.Models;

namespace TheSexy6BotWorker.Tests.Helpers;

public class ImageCommandParserTests
{
    [Fact]
    public void Parse_WithPlainPrompt_UsesFluxDefault()
    {
        var result = ImageCommandParser.Parse("a", "small lighthouse at dawn");

        Assert.True(result.Success);
        Assert.NotNull(result.Request);
        Assert.Equal("a small lighthouse at dawn", result.Request.Prompt);
        Assert.Null(result.Request.RequestedModel);
    }

    [Fact]
    public void Parse_WithSeedreamAlias_UsesRequestedModel()
    {
        var result = ImageCommandParser.Parse("seedream", "cinematic train station");

        Assert.True(result.Success);
        Assert.NotNull(result.Request);
        Assert.Equal("cinematic train station", result.Request.Prompt);
        Assert.Equal(ImageGenerationModelChoice.Seedream, result.Request.RequestedModel);
    }

    [Fact]
    public void Parse_WithAliasAndNoPrompt_ReturnsUsageFailure()
    {
        var result = ImageCommandParser.Parse("flux", null);

        Assert.False(result.Success);
        Assert.Null(result.Request);
        Assert.Equal(ImageCommandParser.Usage, result.Usage);
        Assert.Contains("prompt", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
