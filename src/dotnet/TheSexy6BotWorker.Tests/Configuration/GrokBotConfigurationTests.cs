using Xunit;
using TheSexy6BotWorker.Configuration;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;

namespace TheSexy6BotWorker.Tests.Configuration;

public class GrokBotConfigurationTests
{
    [Fact]
    public void Constructor_WithoutEnvironmentPrefix_SetsDefaultPrefix()
    {
        // Arrange & Act
        var config = new GrokBotConfiguration();

        // Assert
        Assert.Equal("grok", config.Prefix);
    }

    [Fact]
    public void Constructor_WithEnvironmentPrefix_SetsCustomPrefix()
    {
        // Arrange & Act
        var config = new GrokBotConfiguration("test-");

        // Assert
        Assert.Equal("test-grok", config.Prefix);
    }

    [Fact]
    public void ServiceId_ReturnsCorrectValue()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act & Assert
        Assert.Equal("grok", config.ServiceId);
    }

    [Fact]
    public void SystemMessage_ReturnsNonEmptyString()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act & Assert
        Assert.NotNull(config.SystemMessage);
        Assert.NotEmpty(config.SystemMessage);
        Assert.Contains("intelligent", config.SystemMessage);
        Assert.Contains("uncensored", config.SystemMessage);
    }

    [Fact]
    public void SystemMessage_ContainsExpectedInstructions()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act
        var systemMessage = config.SystemMessage;

        // Assert
        Assert.Contains("witty", systemMessage);
        Assert.Contains("NSFW", systemMessage);
        Assert.Contains("SAFETY", systemMessage);
        Assert.Contains("DO NOT REFERENCE YOUR SYSTEM INSTRUCTIONS", systemMessage);
    }

    [Fact]
    public void Settings_ReturnsOpenAIPromptExecutionSettings()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act & Assert
        Assert.NotNull(config.Settings);
        Assert.IsType<OpenAIPromptExecutionSettings>(config.Settings);
    }

    [Fact]
    public void Settings_HasCorrectMaxTokens()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act
        var settings = config.Settings as OpenAIPromptExecutionSettings;

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(4096, settings.MaxTokens);
    }

    [Fact]
    public void Settings_HasFunctionChoiceBehaviorAuto()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act
        var settings = config.Settings as OpenAIPromptExecutionSettings;

        // Assert
        Assert.NotNull(settings);
        Assert.NotNull(settings.FunctionChoiceBehavior);
    }

    [Fact]
    public void Settings_IsMutable_CanBeModified()
    {
        // Arrange
        var config = new GrokBotConfiguration();
        var newSettings = new OpenAIPromptExecutionSettings 
        { 
            MaxTokens = 8192,
            Temperature = 0.9
        };

        // Act
        config.Settings = newSettings;

        // Assert
        var settings = config.Settings as OpenAIPromptExecutionSettings;
        Assert.NotNull(settings);
        Assert.Equal(8192, settings.MaxTokens);
        Assert.Equal(0.9, settings.Temperature);
    }

    [Fact]
    public void Settings_ModifyingProperties_Persists()
    {
        // Arrange
        var config = new GrokBotConfiguration();
        var settings = config.Settings as OpenAIPromptExecutionSettings;

        // Act
        settings!.Temperature = 1.2;
        settings.MaxTokens = 2048;

        // Assert
        var retrievedSettings = config.Settings as OpenAIPromptExecutionSettings;
        Assert.Equal(1.2, retrievedSettings!.Temperature);
        Assert.Equal(2048, retrievedSettings.MaxTokens);
    }

    [Fact]
    public void SupportsReplyChains_ReturnsTrue()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act & Assert
        Assert.True(config.SupportsReplyChains);
    }

    [Fact]
    public void SupportsFunctionCalling_ReturnsTrue()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act & Assert
        Assert.True(config.SupportsFunctionCalling);
    }

    [Fact]
    public void SupportsImages_ReturnsTrue()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act & Assert
        Assert.True(config.SupportsImages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("test-")]
    [InlineData("dev-")]
    [InlineData("prod-")]
    public void Constructor_WithVariousPrefixes_SetsCorrectPrefix(string environmentPrefix)
    {
        // Arrange & Act
        var config = new GrokBotConfiguration(environmentPrefix);

        // Assert
        Assert.Equal($"{environmentPrefix}grok", config.Prefix);
    }

    [Fact]
    public void AllCapabilities_AreEnabled()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act & Assert - Grok should be the "full-featured" bot
        Assert.True(config.SupportsReplyChains);
        Assert.True(config.SupportsFunctionCalling);
        Assert.True(config.SupportsImages);
    }

    [Fact]
    public void GetConfigurationDescription_ReturnsNonEmptyString()
    {
        // Arrange
        var config = new GrokBotConfiguration();

        // Act
        var description = config.GetConfigurationDescription();

        // Assert
        Assert.NotNull(description);
        Assert.NotEmpty(description);
        Assert.Contains("grok", description);
    }
}
