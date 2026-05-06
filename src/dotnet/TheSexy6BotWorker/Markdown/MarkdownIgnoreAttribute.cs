namespace TheSexy6BotWorker.Markdown;

/// <summary>
/// Excludes a property from markdown generation
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class MarkdownIgnoreAttribute : Attribute
{
}
