namespace TheSexy6BotWorker.Markdown;

/// <summary>
/// Controls how a property is rendered in markdown
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class MarkdownPropertyAttribute : Attribute
{
    /// <summary>
    /// The display label for this property (defaults to property name)
    /// </summary>
    public string? Label { get; set; }
    
    /// <summary>
    /// Display order within a section (lower numbers appear first)
    /// </summary>
    public int Order { get; set; } = 0;
    
    /// <summary>
    /// Whether to render as bold text
    /// </summary>
    public bool Bold { get; set; } = true;
    
    /// <summary>
    /// Format string for the value (e.g., "F2" for decimals)
    /// </summary>
    public string? Format { get; set; }
    
    /// <summary>
    /// Whether to skip this property if its value is null
    /// </summary>
    public bool SkipIfNull { get; set; } = true;
    
    /// <summary>
    /// Custom icon/emoji to display before the label
    /// </summary>
    public string? Icon { get; set; }
}
