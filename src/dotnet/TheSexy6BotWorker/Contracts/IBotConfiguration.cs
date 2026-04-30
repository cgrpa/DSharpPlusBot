using System.ComponentModel;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Markdown;

namespace TheSexy6BotWorker.Contracts;

public interface IBotConfiguration
{
    /// <summary>
    /// The command prefix that triggers this bot (e.g., "gemini", "grok")
    /// </summary>
    [Description("The command prefix that triggers this bot")]
    [MarkdownProperty(Label = "Prefix", Icon = "🤖")]
    string Prefix { get; }
    
    /// <summary>
    /// The Semantic Kernel service ID for the chat completion service
    /// </summary>
    [Description("The Semantic Kernel service ID for the chat completion service")]
    [MarkdownProperty(Label = "Service ID", Icon = "🔧")]
    string ServiceId { get; }
    
    /// <summary>
    /// The system message that defines the bot's personality and behavior
    /// </summary>
    [Description("The system message that defines the bot's personality and behavior")]
    [MarkdownIgnore] // Too long for markdown output
    string SystemMessage { get; }
    
    /// <summary>
    /// Prompt execution settings (mutable for runtime configuration changes)
    /// </summary>
    [Description("Prompt execution settings for the LLM")]
    [MarkdownProperty(Label = "Settings Type", Icon = "⚙️")]
    PromptExecutionSettings Settings { get; set; }
    
    /// <summary>
    /// Whether this bot supports threaded reply chains for conversation context
    /// </summary>
    [Description("Enables conversation history tracking through Discord reply chains")]
    [MarkdownProperty(Label = "Reply Chains", Icon = "💬")]
    bool SupportsReplyChains { get; }
    
    /// <summary>
    /// Whether this bot supports automatic function calling via Semantic Kernel plugins
    /// </summary>
    [Description("Enables automatic function calling via Semantic Kernel plugins for tools like weather and search")]
    [MarkdownProperty(Label = "Function Calling", Icon = "🛠️")]
    bool SupportsFunctionCalling { get; }
    
    /// <summary>
    /// Whether this bot can process image attachments in messages
    /// </summary>
    [Description("Enables processing of image attachments in messages for vision capabilities")]
    [MarkdownProperty(Label = "Image Processing", Icon = "🖼️")]
    bool SupportsImages { get; }

    #region Engagement Mode Configuration
    
    /// <summary>
    /// Whether this bot supports engagement mode (continuous conversation without prefix)
    /// </summary>
    [Description("Enables engagement mode for continuous conversation without requiring prefix")]
    [MarkdownProperty(Label = "Engagement Mode", Icon = "🎯")]
    bool SupportsEngagementMode { get; }
    
    /// <summary>
    /// Additional system instructions injected when engagement mode is active.
    /// Should define the bot's personality for autonomous conversation decisions.
    /// </summary>
    [MarkdownIgnore]
    string? EngagementModeInstructions { get; }
    
    /// <summary>
    /// How long a session stays active without new messages
    /// </summary>
    [Description("Session timeout duration")]
    [MarkdownProperty(Label = "Session Timeout", Icon = "⏱️")]
    TimeSpan SessionTimeout { get; }
    
    /// <summary>
    /// Number of messages in the activity window that triggers rate limiting
    /// </summary>
    [Description("Message threshold for high activity detection")]
    [MarkdownProperty(Label = "High Activity Threshold", Icon = "📊")]
    int HighActivityThreshold { get; }
    
    /// <summary>
    /// Time window for measuring high activity
    /// </summary>
    [Description("Time window for high activity measurement")]
    [MarkdownProperty(Label = "Activity Window", Icon = "🕐")]
    TimeSpan HighActivityWindow { get; }
    
    /// <summary>
    /// Minimum delay before replying during high activity
    /// </summary>
    TimeSpan HighActivityDelayMin { get; }
    
    /// <summary>
    /// Maximum delay before replying during high activity
    /// </summary>
    TimeSpan HighActivityDelayMax { get; }
    
    #endregion

    /// <summary>
    /// Gets a description of the bot's configuration and capabilities using reflection
    /// </summary>
    string GetConfigurationDescription();
}
