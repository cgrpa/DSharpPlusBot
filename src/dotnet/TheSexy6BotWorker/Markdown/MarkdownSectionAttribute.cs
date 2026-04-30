namespace TheSexy6BotWorker.Markdown;

/// <summary>
/// Marks a property or class for inclusion in markdown output with a specific section heading
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = false)]
public class MarkdownSectionAttribute : Attribute
{
    /// <summary>
    /// The heading text for this section
    /// </summary>
    public string Heading { get; }
    
    /// <summary>
    /// The heading level (1-6, corresponding to # through ######)
    /// </summary>
    public int Level { get; set; } = 2;
    
    /// <summary>
    /// Display order for this section (lower numbers appear first)
    /// </summary>
    public int Order { get; set; } = 0;

    public MarkdownSectionAttribute(string heading)
    {
        Heading = heading;
    }
}
