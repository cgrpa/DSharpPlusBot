using Xunit;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Contracts;
using Xunit.Abstractions;

namespace TheSexy6BotWorker.Tests.Integration;

/// <summary>
/// Integration tests demonstrating the complete markdown generation workflow
/// </summary>
public class MarkdownIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public MarkdownIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GeminiBotConfiguration_GeneratesWellStructuredMarkdown()
    {
        // Arrange
        IBotConfiguration config = new GeminiBotConfiguration();

        // Act
        var markdown = config.GetConfigurationDescription();

        // Assert - Verify markdown structure
        Assert.Contains("## Bot Configuration", markdown);
        Assert.Contains("## Capabilities", markdown);
        Assert.Contains("## Execution Settings", markdown);
        
        // Verify emojis/icons
        Assert.Contains("🤖", markdown);
        Assert.Contains("💬", markdown);
        Assert.Contains("🛠️", markdown);
        Assert.Contains("🖼️", markdown);
        Assert.Contains("⚙️", markdown);
        
        // Verify capability states
        Assert.Contains("❌ Disabled", markdown);
        
        // Verify settings reflection worked
        Assert.Contains("GeminiPromptExecutionSettings", markdown);
        Assert.Contains("MaxTokens", markdown);
        Assert.Contains("4096", markdown);
        
        // Output for visual verification
        _output.WriteLine("=== Gemini Bot Configuration ===");
        _output.WriteLine(markdown);
    }

    [Fact]
    public void GrokBotConfiguration_GeneratesWellStructuredMarkdown()
    {
        // Arrange
        IBotConfiguration config = new GrokBotConfiguration();

        // Act
        var markdown = config.GetConfigurationDescription();

        // Assert - Verify markdown structure
        Assert.Contains("## Bot Configuration", markdown);
        Assert.Contains("## Capabilities", markdown);
        Assert.Contains("## Execution Settings", markdown);
        
        // Verify all capabilities are enabled
        Assert.Contains("✅ Enabled", markdown);
        
        // Verify settings reflection worked
        Assert.Contains("OpenAIPromptExecutionSettings", markdown);
        Assert.Contains("MaxTokens", markdown);
        
        // Output for visual verification
        _output.WriteLine("=== Grok Bot Configuration ===");
        _output.WriteLine(markdown);
    }

    [Fact]
    public void BotConfiguration_WithEnvironmentPrefix_ReflectsInMarkdown()
    {
        // Arrange
        IBotConfiguration config = new GrokBotConfiguration("test-");

        // Act
        var markdown = config.GetConfigurationDescription();

        // Assert
        Assert.Contains("test-grok", markdown);
        
        _output.WriteLine("=== Test Environment Bot ===");
        _output.WriteLine(markdown);
    }

    [Fact]
    public void BotConfiguration_ModifiedSettings_ReflectsInMarkdown()
    {
        // Arrange
        var config = new GrokBotConfiguration();
        var settings = config.Settings as Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings;
        
        // Act - Modify settings
        settings!.Temperature = 0.8;
        settings.MaxTokens = 2048;
        
        var markdown = config.GetConfigurationDescription();

        // Assert - Modified values should appear
        Assert.Contains("0.8", markdown);
        Assert.Contains("2048", markdown);
        Assert.DoesNotContain("4096", markdown); // Old value should not appear
        
        _output.WriteLine("=== Modified Settings ===");
        _output.WriteLine(markdown);
    }

    [Fact]
    public void MarkdownLibrary_ProducesConsistentOutput()
    {
        // Arrange
        var config1 = new GeminiBotConfiguration();
        var config2 = new GeminiBotConfiguration();

        // Act
        var markdown1 = config1.GetConfigurationDescription();
        var markdown2 = config2.GetConfigurationDescription();

        // Assert - Same configuration should produce identical markdown
        Assert.Equal(markdown1, markdown2);
    }
}
