using Xunit;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Contracts;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace TheSexy6BotWorker.Tests.Configuration;

public class BotConfigurationExtensionsTests
{
    [Fact]
    public void GenerateConfigurationDescription_GeminiBot_ReturnsCompleteDescription()
    {
        // Arrange
        var config = new GeminiBotConfiguration();

        // Act
        var description = config.GenerateConfigurationDescription();

        // Assert
        Assert.NotNull(description);
        Assert.Contains("## Bot Configuration", description);
        Assert.Contains("gemini", description);
        Assert.Contains("## Capabilities", description);
        Assert.Contains("Reply Chains", description);
        Assert.Contains("Disabled", description);
        Assert.Contains("## Execution Settings", description);
        Assert.Contains("GeminiPromptExecutionSettings", description);
        Assert.Contains("MaxTokens", description);
        Assert.Contains("4096", description);
    }

    [Fact]
    public void GenerateConfigurationDescription_GrokBot_ReturnsCompleteDescription()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act
        var description = config.GenerateConfigurationDescription();

        // Assert
        Assert.NotNull(description);
        Assert.Contains("## Bot Configuration", description);
        Assert.Contains("grok", description);
        Assert.Contains("## Capabilities", description);
        Assert.Contains("Reply Chains", description);
        Assert.Contains("Enabled", description);
        Assert.Contains("Function Calling", description);
        Assert.Contains("Enabled", description);
        Assert.Contains("## Execution Settings", description);
        Assert.Contains("OpenAIPromptExecutionSettings", description);
        Assert.Contains("MaxTokens", description);
    }

    [Fact]
    public void GenerateConfigurationDescription_ReflectsModifiedSettings()
    {
        // Arrange
        var config = new GeminiBotConfiguration();
        var settings = config.Settings as GeminiPromptExecutionSettings;
        settings!.MaxTokens = 8192;

        // Act
        var description = config.GenerateConfigurationDescription();

        // Assert
        Assert.Contains("8192", description);
        Assert.DoesNotContain("4096", description);
    }

    [Fact]
    public void GetConfigurationDescription_InterfaceMethod_WorksForAllBots()
    {
        // Arrange
        IBotConfiguration gemini = new GeminiBotConfiguration();
        IBotConfiguration grok = new GrokBotConfiguration();

        // Act
        var geminiDesc = gemini.GetConfigurationDescription();
        var grokDesc = grok.GetConfigurationDescription();

        // Assert - Both should return non-empty descriptions
        Assert.NotEmpty(geminiDesc);
        Assert.NotEmpty(grokDesc);
        
        // Each should contain their own service ID
        Assert.Contains("gemini", geminiDesc);
        Assert.Contains("grok", grokDesc);
    }

    [Fact]
    public void GenerateConfigurationDescription_IncludesAllCapabilityFlags()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act
        var description = config.GenerateConfigurationDescription();

        // Assert - All three capability flags should be mentioned
        Assert.Contains("Reply Chains", description);
        Assert.Contains("Function Calling", description);
        Assert.Contains("Image Processing", description);
    }

    [Fact]
    public void GenerateConfigurationDescription_HandlesComplexSettings()
    {
        // Arrange
        var config = new GrokBotConfiguration();
        var settings = config.Settings as OpenAIPromptExecutionSettings;
        settings!.Temperature = 0.7;
        settings.TopP = 0.9;

        // Act
        var description = config.GenerateConfigurationDescription();

        // Assert - Should include reflection-discovered properties
        Assert.Contains("Temperature", description);
        Assert.Contains("0.7", description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("test-")]
    [InlineData("dev-")]
    public void GenerateConfigurationDescription_IncludesEnvironmentPrefix(string prefix)
    {
        // Arrange
        var config = new GeminiBotConfiguration(prefix);

        // Act
        var description = config.GenerateConfigurationDescription();

        // Assert
        Assert.Contains($"{prefix}gemini", description);
    }
}
