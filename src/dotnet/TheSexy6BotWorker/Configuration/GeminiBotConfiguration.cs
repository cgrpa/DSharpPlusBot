using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using TheSexy6BotWorker.Contracts;

namespace TheSexy6BotWorker.Configuration;

public class GeminiBotConfiguration : IBotConfiguration
{
    private readonly string _environmentPrefix;

    public GeminiBotConfiguration(string environmentPrefix = "")
    {
        _environmentPrefix = environmentPrefix;
    }

    public string Prefix => $"{_environmentPrefix}gemini";
    
    public string ServiceId => "gemini";
    
    public string SystemMessage => "You are a Discord AI Assistant.";
    
    public PromptExecutionSettings Settings { get; set; } = new GeminiPromptExecutionSettings
    {
        MaxTokens = 4096
    };
    
    public bool SupportsReplyChains => false;
    
    public bool SupportsFunctionCalling => false;
    
    public bool SupportsImages => false;

    #region Engagement Mode Configuration (Disabled)
    
    public bool SupportsEngagementMode => false;
    
    public string? EngagementModeInstructions => null;
    
    public TimeSpan SessionTimeout => TimeSpan.FromMinutes(3);
    
    public int HighActivityThreshold => 5;
    
    public TimeSpan HighActivityWindow => TimeSpan.FromSeconds(15);
    
    public TimeSpan HighActivityDelayMin => TimeSpan.FromSeconds(2);
    
    public TimeSpan HighActivityDelayMax => TimeSpan.FromSeconds(4);
    
    #endregion

    public string GetConfigurationDescription() => this.GenerateConfigurationDescription();
}
