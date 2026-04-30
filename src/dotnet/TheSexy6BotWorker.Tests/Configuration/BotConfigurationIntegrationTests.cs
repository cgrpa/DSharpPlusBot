using Xunit;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Services;
using TheSexy6BotWorker.Contracts;

namespace TheSexy6BotWorker.Tests.Configuration;

/// <summary>
/// Integration tests for bot configurations working with the registry
/// </summary>
public class BotConfigurationIntegrationTests
{
    [Fact]
    public void Registry_WithMultipleBots_CanDistinguishByPrefix()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration());
        registry.Register(new GrokBotConfiguration());

        // Act
        var geminiFound = registry.TryGetBot("gemini hello", out var geminiBot, out var geminiMessage);
        var grokFound = registry.TryGetBot("grok hello", out var grokBot, out var grokMessage);

        // Assert
        Assert.True(geminiFound);
        Assert.True(grokFound);
        Assert.NotEqual(geminiBot?.ServiceId, grokBot?.ServiceId);
        Assert.Equal("hello", geminiMessage);
        Assert.Equal("hello", grokMessage);
    }

    [Fact]
    public void Registry_WithEnvironmentPrefixes_HandlesCollisions()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration("test-"));
        
        // Act - trying to register same bot with same prefix should throw
        var exception = Assert.Throws<InvalidOperationException>(() => 
            registry.Register(new GeminiBotConfiguration("test-")));

        // Assert
        Assert.Contains("already registered", exception.Message);
    }

    [Fact]
    public void GeminiBot_ComparedToGrokBot_HasDifferentCapabilities()
    {
        // Arrange
        var gemini = new GeminiBotConfiguration();
        var grok = new GrokBotConfiguration();

        // Assert - Gemini is simpler
        Assert.False(gemini.SupportsReplyChains);
        Assert.False(gemini.SupportsFunctionCalling);
        Assert.False(gemini.SupportsImages);

        // Assert - Grok has all features
        Assert.True(grok.SupportsReplyChains);
        Assert.True(grok.SupportsFunctionCalling);
        Assert.True(grok.SupportsImages);
    }

    [Fact]
    public void BotSettings_CanBeModifiedAtRuntime()
    {
        // Arrange
        var registry = new BotRegistry();
        var gemini = new GeminiBotConfiguration();
        registry.Register(gemini);

        // Act - Modify settings after registration
        var bot = registry.GetBot("gemini");
        var originalSettings = bot!.Settings;
        
        // Modify via the bot retrieved from registry
        bot.Settings = new Microsoft.SemanticKernel.Connectors.Google.GeminiPromptExecutionSettings
        {
            MaxTokens = 8192
        };

        // Assert - Changes should be reflected
        var retrievedBot = registry.GetBot("gemini");
        var newSettings = retrievedBot!.Settings as Microsoft.SemanticKernel.Connectors.Google.GeminiPromptExecutionSettings;
        Assert.NotNull(newSettings);
        Assert.Equal(8192, newSettings.MaxTokens);
    }

    [Theory]
    [InlineData("gemini what is AI?", "gemini", "what is AI?")]
    [InlineData("grok tell me a joke", "grok", "tell me a joke")]
    [InlineData("GEMINI case test", "gemini", "case test")]
    [InlineData("test-grok hello", "test-grok", "hello")]
    public void Registry_MessageParsing_HandlesVariousFormats(string message, string expectedPrefix, string expectedStripped)
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration());
        registry.Register(new GrokBotConfiguration());
        registry.Register(new GrokBotConfiguration("test-"));

        // Act
        var found = registry.TryGetBot(message, out var bot, out var strippedMessage);

        // Assert
        Assert.True(found);
        Assert.Equal(expectedPrefix, bot!.Prefix);
        Assert.Equal(expectedStripped, strippedMessage);
    }

    [Fact]
    public void Registry_GetAllBots_PreservesCapabilityInformation()
    {
        // Arrange
        var registry = new BotRegistry();
        registry.Register(new GeminiBotConfiguration());
        registry.Register(new GrokBotConfiguration());

        // Act
        var allBots = registry.GetAllBots();

        // Assert
        Assert.Equal(2, allBots.Count);
        
        var geminiBot = allBots.First(b => b.Prefix == "gemini");
        var grokBot = allBots.First(b => b.Prefix == "grok");

        // Verify capabilities are preserved
        Assert.False(geminiBot.SupportsFunctionCalling);
        Assert.True(grokBot.SupportsFunctionCalling);
    }

    [Fact]
    public void BotConfiguration_SettingsMutation_AffectsAllReferences()
    {
        // Arrange
        var config = new GrokBotConfiguration();
        var settings = config.Settings as Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings;

        // Act - Modify directly
        settings!.Temperature = 0.5;

        // Assert - Changes visible through the config object
        var retrievedSettings = config.Settings as Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings;
        Assert.Equal(0.5, retrievedSettings!.Temperature);
    }
}
