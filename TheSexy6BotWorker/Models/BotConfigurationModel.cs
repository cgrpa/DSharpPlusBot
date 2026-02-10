using System.ComponentModel;
using TheSexy6BotWorker.Markdown;

namespace TheSexy6BotWorker.Models;

/// <summary>
/// Structured representation of bot configuration for markdown generation
/// </summary>
public class BotConfigurationModel
{
    [MarkdownSection("Bot Configuration", Level = 2, Order = 1)]
    [MarkdownProperty(Icon = "🤖", Order = 1)]
    [Description("The command prefix that triggers this bot")]
    public string Prefix { get; set; } = string.Empty;
    
    [MarkdownSection("Bot Configuration")]
    [MarkdownProperty(Icon = "🔌", Order = 2)]
    [Description("The Semantic Kernel service identifier")]
    public string ServiceId { get; set; } = string.Empty;
    
    [MarkdownSection("Capabilities", Level = 2, Order = 2)]
    [MarkdownProperty(Icon = "💬", Label = "Reply Chains", Order = 1)]
    [Description("Supports threaded conversation history")]
    public string ReplyChains { get; set; } = string.Empty;
    
    [MarkdownSection("Capabilities")]
    [MarkdownProperty(Icon = "🛠️", Label = "Function Calling", Order = 2)]
    [Description("Supports automatic function calling and tool usage")]
    public string FunctionCalling { get; set; } = string.Empty;
    
    [MarkdownSection("Capabilities")]
    [MarkdownProperty(Icon = "🖼️", Label = "Image Processing", Order = 3)]
    [Description("Can understand and process image attachments")]
    public string ImageProcessing { get; set; } = string.Empty;
    
    [MarkdownSection("Execution Settings", Level = 2, Order = 3)]
    [MarkdownProperty(Icon = "⚙️", Label = "Settings Type", Order = 1)]
    [Description("The type of prompt execution settings")]
    public string SettingsType { get; set; } = string.Empty;
    
    // Dynamic properties will be added here during generation
    public Dictionary<string, object> AdditionalSettings { get; set; } = new();
}
