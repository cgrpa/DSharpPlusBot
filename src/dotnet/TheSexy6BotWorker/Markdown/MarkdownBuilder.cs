using System.Text;

namespace TheSexy6BotWorker.Markdown;

/// <summary>
/// Fluent API for building markdown documents
/// </summary>
public class MarkdownBuilder
{
    private readonly StringBuilder _builder = new();
    
    public MarkdownBuilder Heading(string text, int level = 1)
    {
        if (level < 1 || level > 6)
            throw new ArgumentOutOfRangeException(nameof(level), "Heading level must be between 1 and 6");
        
        _builder.AppendLine($"{new string('#', level)} {text}");
        return this;
    }
    
    public MarkdownBuilder Text(string text)
    {
        _builder.AppendLine(text);
        return this;
    }
    
    public MarkdownBuilder Bold(string text)
    {
        _builder.Append($"**{text}**");
        return this;
    }
    
    public MarkdownBuilder Italic(string text)
    {
        _builder.Append($"*{text}*");
        return this;
    }
    
    public MarkdownBuilder Code(string code)
    {
        _builder.Append($"`{code}`");
        return this;
    }
    
    public MarkdownBuilder CodeBlock(string code, string? language = null)
    {
        _builder.AppendLine($"```{language ?? string.Empty}");
        _builder.AppendLine(code);
        _builder.AppendLine("```");
        return this;
    }
    
    public MarkdownBuilder ListItem(string text, int indentLevel = 0)
    {
        var indent = new string(' ', indentLevel * 2);
        _builder.AppendLine($"{indent}- {text}");
        return this;
    }
    
    public MarkdownBuilder BulletPoint(string label, object? value, bool boldLabel = true)
    {
        if (value == null)
            return this;
        
        var labelText = boldLabel ? $"**{label}**" : label;
        _builder.AppendLine($"- {labelText}: {value}");
        return this;
    }
    
    public MarkdownBuilder EmptyLine()
    {
        _builder.AppendLine();
        return this;
    }
    
    public MarkdownBuilder Append(string text)
    {
        _builder.Append(text);
        return this;
    }
    
    public MarkdownBuilder AppendLine(string text)
    {
        _builder.AppendLine(text);
        return this;
    }
    
    public MarkdownBuilder Link(string text, string url)
    {
        _builder.Append($"[{text}]({url})");
        return this;
    }
    
    public MarkdownBuilder Image(string altText, string url)
    {
        _builder.AppendLine($"![{altText}]({url})");
        return this;
    }
    
    public MarkdownBuilder Quote(string text)
    {
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            _builder.AppendLine($"> {line}");
        }
        return this;
    }
    
    public MarkdownBuilder HorizontalRule()
    {
        _builder.AppendLine("---");
        return this;
    }
    
    public MarkdownBuilder Table(Action<MarkdownTableBuilder> configure)
    {
        var tableBuilder = new MarkdownTableBuilder(this);
        configure(tableBuilder);
        return this;
    }
    
    public override string ToString() => _builder.ToString();
    
    public void Clear() => _builder.Clear();
}

public class MarkdownTableBuilder
{
    private readonly MarkdownBuilder _markdownBuilder;
    private readonly List<string> _headers = new();
    private readonly List<List<string>> _rows = new();
    
    internal MarkdownTableBuilder(MarkdownBuilder markdownBuilder)
    {
        _markdownBuilder = markdownBuilder;
    }
    
    public MarkdownTableBuilder AddHeader(params string[] headers)
    {
        _headers.AddRange(headers);
        return this;
    }
    
    public MarkdownTableBuilder AddRow(params string[] cells)
    {
        _rows.Add(new List<string>(cells));
        return this;
    }
    
    internal void Build()
    {
        if (_headers.Count == 0)
            return;
        
        // Header row
        _markdownBuilder.Append("| ").Append(string.Join(" | ", _headers)).AppendLine(" |");
        
        // Separator row
        _markdownBuilder.Append("| ").Append(string.Join(" | ", _headers.Select(_ => "---"))).AppendLine(" |");
        
        // Data rows
        foreach (var row in _rows)
        {
            var paddedRow = row.Concat(Enumerable.Repeat(string.Empty, Math.Max(0, _headers.Count - row.Count)));
            _markdownBuilder.Append("| ").Append(string.Join(" | ", paddedRow)).AppendLine(" |");
        }
        
        _markdownBuilder.EmptyLine();
    }
}
