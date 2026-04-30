using Xunit;
using TheSexy6BotWorker.Services;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Contracts;
using Microsoft.SemanticKernel;

namespace TheSexy6BotWorker.Tests.Services;

public class BotRegistryTests
{
    [Fact]
    public void Register_ValidBot_AddsToRegistry()
    {
        // Arrange
        var registry = new BotRegistry();
        var bot = new GeminiBotConfiguration();

        // Act
        registry.Register(bot);
        var result = registry.GetBot(bot.Prefix);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(bot.Prefix, result.Prefix);
        Assert.Equal(bot.ServiceId, result.ServiceId);
    }

    [Fact]
    public void Register_NullBot_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new BotRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Fact]
    public void Register_DuplicatePrefix_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new BotRegistry();
        var bot1 = new GeminiBotConfiguration();
        var bot2 = new GeminiBotConfiguration(); // Same prefix

        // Act
        registry.Register(bot1);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(() => registry.Register(bot2));
        Assert.Contains("already registered", exception.Message);
        Assert.Contains(bot1.Prefix, exception.Message);
    }

    [Fact]
    public void TryGetBot_MatchingPrefix_ReturnsTrueAndBot()
    {
        // Arrange
        var registry = new BotRegistry();
        var bot = new GrokBotConfiguration();
        registry.Register(bot);
        var message = "grok tell me a joke";

        // Act
        var found = registry.TryGetBot(message, out var result, out var strippedMessage);

        // Assert
        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal("grok", result.Prefix);
        Assert.Equal("tell me a joke", strippedMessage);
    }

    [Fact]
    public void TryGetBot_NoMatchingPrefix_ReturnsFalse()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration());
        var message = "claude tell me something";

        // Act
        var found = registry.TryGetBot(message, out var result, out var strippedMessage);

        // Assert
        Assert.False(found);
        Assert.Null(result);
        Assert.Equal(message, strippedMessage); // Original message unchanged
    }

    [Fact]
    public void TryGetBot_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration());
        var message = "GEMINI what is AI?";

        // Act
        var found = registry.TryGetBot(message, out var result, out var strippedMessage);

        // Assert
        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal("what is AI?", strippedMessage);
    }

    [Fact]
    public void TryGetBot_WithWhitespaceAfterPrefix_TrimsCorrectly()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GrokBotConfiguration());
        var message = "grok    multiple spaces";

        // Act
        var found = registry.TryGetBot(message, out var result, out var strippedMessage);

        // Assert
        Assert.True(found);
        Assert.Equal("multiple spaces", strippedMessage);
    }

    [Fact]
    public void TryGetBot_PrefixOnly_ReturnsEmptyStrippedMessage()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration());
        var message = "gemini";

        // Act
        var found = registry.TryGetBot(message, out var result, out var strippedMessage);

        // Assert
        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal(string.Empty, strippedMessage);
    }

    [Fact]
    public void TryGetBot_NullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            registry.TryGetBot(null!, out _, out _));
    }

    [Fact]
    public void TryGetBot_EmptyMessage_ThrowsArgumentException()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration());

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            registry.TryGetBot(string.Empty, out _, out _));
    }

    [Fact]
    public void GetAllBots_MultipleBotsRegistered_ReturnsAll()
    {
        // Arrange
        var registry = new BotRegistry();
        var gemini = new GeminiBotConfiguration();
        var grok = new GrokBotConfiguration();
        registry.Register(gemini);
        registry.Register(grok);

        // Act
        var allBots = registry.GetAllBots();

        // Assert
        Assert.Equal(2, allBots.Count);
        Assert.Contains(allBots, b => b.Prefix == "gemini");
        Assert.Contains(allBots, b => b.Prefix == "grok");
    }

    [Fact]
    public void GetAllBots_EmptyRegistry_ReturnsEmptyCollection()
    {
        // Arrange
        var registry = new BotRegistry();

        // Act
        var allBots = registry.GetAllBots();

        // Assert
        Assert.NotNull(allBots);
        Assert.Empty(allBots);
    }

    [Fact]
    public void GetAllBots_ReturnsReadOnlyCollection()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration());

        // Act
        var allBots = registry.GetAllBots();

        // Assert
        Assert.IsAssignableFrom<IReadOnlyCollection<IBotConfiguration>>(allBots);
    }

    [Fact]
    public void GetBot_ExistingPrefix_ReturnsBot()
    {
        // Arrange
        var registry = new BotRegistry();
        var bot = new GrokBotConfiguration();
        registry.Register(bot);

        // Act
        var result = registry.GetBot("grok");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("grok", result.Prefix);
    }

    [Fact]
    public void GetBot_NonExistentPrefix_ReturnsNull()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration());

        // Act
        var result = registry.GetBot("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetBot_CaseInsensitive_ReturnsBot()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration());

        // Act
        var result = registry.GetBot("GEMINI");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("gemini", result.Prefix);
    }

    [Fact]
    public void TryGetBot_WithEnvironmentPrefix_MatchesCorrectly()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration("test-"));
        var message = "test-gemini hello world";

        // Act
        var found = registry.TryGetBot(message, out var result, out var strippedMessage);

        // Assert
        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal("test-gemini", result.Prefix);
        Assert.Equal("hello world", strippedMessage);
    }

    [Fact]
    public void TryGetBot_PartialPrefixMatch_DoesNotMatch()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration());
        var message = "gem not a full match"; // "gem" is not "gemini"

        // Act
        var found = registry.TryGetBot(message, out var result, out _);

        // Assert
        Assert.False(found);
        Assert.Null(result);
    }
}
