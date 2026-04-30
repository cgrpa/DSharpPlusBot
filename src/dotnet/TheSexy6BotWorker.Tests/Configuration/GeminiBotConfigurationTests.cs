using Xunit;
using TheSexy6BotWorker.Configuration;
using Microsoft.SemanticKernel.Connectors.Google;

namespace TheSexy6BotWorker.Tests.Configuration;

public class GeminiBotConfigurationTests
{
    [Fact]
    public void Constructor_WithoutEnvironmentPrefix_SetsDefaultPrefix()
    {
        // Arrange & Act
        var config = new GeminiBotConfiguration();

        // Assert
        Assert.Equal("gemini", config.Prefix);
    }

    [Fact]
    public void Constructor_WithEnvironmentPrefix_SetsCustomPrefix()
    {
        // Arrange & Act
        var config = new GeminiBotConfiguration("test-");

        // Assert
        Assert.Equal("test-gemini", config.Prefix);
    }

    [Fact]
    public void ServiceId_ReturnsCorrectValue()
    {
        // Arrange
        var config = new GeminiBotConfiguration();

        // Act & Assert
        Assert.Equal("gemini", config.ServiceId);
    }

    [Fact]
    public void SystemMessage_ReturnsNonEmptyString()
    {
        // Arrange
        var config = new GeminiBotConfiguration();

        // Act & Assert
        Assert.NotNull(config.SystemMessage);
        Assert.NotEmpty(config.SystemMessage);
        Assert.Contains("Discord AI Assistant", config.SystemMessage);
    }

    [Fact]
    public void Settings_ReturnsGeminiPromptExecutionSettings()
    {
        // Arrange
        var config = new GeminiBotConfiguration();

        // Act & Assert
        Assert.NotNull(config.Settings);
        Assert.IsType<GeminiPromptExecutionSettings>(config.Settings);
    }

    [Fact]
    public void Settings_HasCorrectMaxTokens()
    {
        // Arrange
        var config = new GeminiBotConfiguration();

        // Act
        var settings = config.Settings as GeminiPromptExecutionSettings;

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(4096, settings.MaxTokens);
    }

    [Fact]
    public void Settings_IsMutable_CanBeModified()
    {
        // Arrange
        var config = new GeminiBotConfiguration();
        var newSettings = new GeminiPromptExecutionSettings { MaxTokens = 8192 };

        // Act
        config.Settings = newSettings;

        // Assert
        var settings = config.Settings as GeminiPromptExecutionSettings;
        Assert.NotNull(settings);
        Assert.Equal(8192, settings.MaxTokens);
    }

    [Fact]
    public void SupportsReplyChains_ReturnsFalse()
    {
        // Arrange
        var config = new GeminiBotConfiguration();

        // Act & Assert
        Assert.False(config.SupportsReplyChains);
    }

    [Fact]
    public void SupportsFunctionCalling_ReturnsFalse()
    {
        // Arrange
        var config = new GeminiBotConfiguration();

        // Act & Assert
        Assert.False(config.SupportsFunctionCalling);
    }

    [Fact]
    public void SupportsImages_ReturnsFalse()
    {
        // Arrange
        var config = new GeminiBotConfiguration();

        // Act & Assert
        Assert.False(config.SupportsImages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("test-")]
    [InlineData("dev-")]
    [InlineData("prod-")]
    public void Constructor_WithVariousPrefixes_SetsCorrectPrefix(string environmentPrefix)
    {
        // Arrange & Act
        var config = new GeminiBotConfiguration(environmentPrefix);

        // Assert
        Assert.Equal($"{environmentPrefix}gemini", config.Prefix);
    }

    [Fact]
    public void GetConfigurationDescription_ReturnsNonEmptyString()
    {
        // Arrange
        var config = new GeminiBotConfiguration();

        // Act
        var description = config.GetConfigurationDescription();

        // Assert
        Assert.NotNull(description);
        Assert.NotEmpty(description);
        Assert.Contains("gemini", description);
    }
}
