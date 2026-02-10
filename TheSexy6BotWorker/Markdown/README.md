# Markdown Generation Library

A fluent markdown generation library for .NET that gives you full control over what properties to include in your output. Uses explicit property selection with optional attribute-based metadata.

## Features

- **Fluent Builder API**: Chain methods to build markdown programmatically
- **Expression-Based Selection**: Use lambda expressions to select properties explicitly
- **Selective Inclusion**: You choose exactly which properties appear in the output
- **Optional Attributes**: `[MarkdownProperty]`, `[Description]`, and `[MarkdownIgnore]` provide metadata hints
- **Emoji Support**: Add icons to properties for visual clarity
- **Formatting**: Support for format strings on numeric properties
- **Conditional Properties**: Include properties based on runtime conditions
- **Reflection Support**: Optionally reflect dynamic objects with filtering

## Quick Start

### Basic MarkdownBuilder

```csharp
var markdown = new MarkdownBuilder()
    .Heading("Title", 1)
    .Text("Some introductory text")
    .Heading("Features", 2)
    .BulletPoint("Name", "Value")
    .BulletPoint("Count", 42)
    .EmptyLine()
    .Bold("Important: ")
    .Append("This is highlighted text")
    .ToString();
```

**Output:**
```markdown
# Title
Some introductory text
## Features
- **Name**: Value
- **Count**: 42

**Important:** This is highlighted text
```

### ObjectMarkdownBuilder - Fluent Property Selection

The primary API for generating markdown from objects:

```csharp
public class ServerConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
    public bool Active { get; set; } = true;
    public string InternalSecret { get; set; } = "hidden";
}

var config = new ServerConfig();

var markdown = new ObjectMarkdownBuilder<ServerConfig>(config)
    .Section("Server Details", section => section
        .Property(c => c.Host, icon: "🌐")
        .Property(c => c.Port, icon: "🔌"))
    .Section("Status", section => section
        .Property(c => c.Active, label: "Is Running", icon: "✅"))
    // InternalSecret is NOT included - we choose what to show
    .Build();
```

**Output:**
```markdown
## Server Details
- **🌐 Host**: localhost
- **🔌 Port**: 8080

## Status
- **✅ Is Running**: True
```

### Using Attributes as Metadata

You can optionally add attributes and read them with `PropertyWithAttributes`:

```csharp
public class Config
{
    [MarkdownProperty(Label = "Server Host", Icon = "🌐")]
    public string Host { get; set; } = "localhost";
    
    [MarkdownProperty(Icon = "🔌")]
    public int Port { get; set; } = 8080;
}

var config = new Config();
var markdown = new ObjectMarkdownBuilder<Config>(config)
    .Section("Server")
    .PropertyWithAttributes(c => c.Host)  // Uses label and icon from attribute
    .PropertyWithAttributes(c => c.Port)
    .Build();
```

## ObjectMarkdownBuilder API

### Creating a Builder

```csharp
var builder = new ObjectMarkdownBuilder<MyClass>(instance);
```

### Adding Sections

```csharp
// Simple section heading
.Section("Configuration", level: 2)

// Section with properties
.Section("Configuration", section => section
    .Property(x => x.Name)
    .Property(x => x.Value))
```

### Adding Properties

```csharp
// Basic property
.Property(x => x.Name)

// With custom label
.Property(x => x.Name, label: "Display Name")

// With icon
.Property(x => x.Name, icon: "🏷️")

// With format string
.Property(x => x.Temperature, format: "F2")

// Full customization
.Property(x => x.Value, label: "Custom", icon: "⚙️", format: "N0", bold: true)

// Using attributes from the property
.PropertyWithAttributes(x => x.Name)

// Conditional property
section.PropertyIf(showAdvanced, x => x.AdvancedSetting)
```

### Other Methods

```csharp
// Add manual bullet point
.BulletPoint("Label", value, icon: "🔹")

// Add raw text
.Text("Some description")

// Add empty line
.EmptyLine()

// Reflect all public properties of a nested object
.ReflectObject(settings)

// Reflect with filter
.ReflectObject(settings, prop => !skipList.Contains(prop.Name))

// Direct access to MarkdownBuilder
.Custom(builder => builder.Heading("Custom Section", 3))

// Build final string
.Build()
```

## Attributes Reference

### `[MarkdownProperty]`

Optional metadata for properties.

**Properties:**
- `Label`: Custom display label (default: property name)
- `Order`: Display order within section (default: 0)
- `Bold`: Whether to bold the label (default: true)
- `Format`: Format string for values (e.g., "F2" for decimals)
- `SkipIfNull`: Skip null values (default: true)
- `Icon`: Emoji/icon to display before label

```csharp
[MarkdownProperty(Label = "Temperature", Icon = "🌡️", Format = "F1")]
public double Temp { get; set; } = 72.5;
```

### `[MarkdownIgnore]`

Excludes a property from `ReflectObject()` operations.

```csharp
[MarkdownIgnore]
public string InternalState { get; set; }
```

### `[Description]`

Standard `System.ComponentModel.DescriptionAttribute` - can be read via reflection for documentation.

```csharp
[Description("Enables conversation history tracking")]
public bool SupportsReplyChains { get; set; }
```

## MarkdownBuilder Methods

### Headings & Text
- `Heading(text, level)` - Add heading (# through ######)
- `Text(text)` - Add paragraph text
- `Bold(text)` - Add bold text
- `Italic(text)` - Add italic text

### Lists
- `ListItem(text, indentLevel)` - Add bullet list item
- `BulletPoint(label, value, boldLabel)` - Add label: value bullet

### Code
- `Code(code)` - Inline code `like this`
- `CodeBlock(code, language)` - Fenced code block with syntax highlighting

### Links & Media
- `Link(text, url)` - Add hyperlink
- `Image(altText, url)` - Add image

### Structure
- `Quote(text)` - Add blockquote (>)
- `HorizontalRule()` - Add horizontal rule (---)
- `EmptyLine()` - Add blank line
- `Append(text)` - Raw text without newline
- `AppendLine(text)` - Raw text with newline

### Advanced
- `Table(configure)` - Build markdown table

## Real-World Example: Bot Configuration

From the actual codebase - generating bot configuration descriptions:

```csharp
// IBotConfiguration interface with attributes
public interface IBotConfiguration
{
    [Description("The command prefix that triggers this bot")]
    [MarkdownProperty(Label = "Prefix", Icon = "🤖")]
    string Prefix { get; }
    
    [Description("Enables conversation history tracking through Discord reply chains")]
    [MarkdownProperty(Label = "Reply Chains", Icon = "💬")]
    bool SupportsReplyChains { get; }
    
    // ... more properties
}

// Extension method using fluent builder
public static string GenerateConfigurationDescription(this IBotConfiguration config)
{
    var builder = new MarkdownBuilder();
    
    builder.Heading("Bot Configuration", 2);
    AddPropertyFromInterface(builder, nameof(IBotConfiguration.Prefix), config.Prefix);
    AddPropertyFromInterface(builder, nameof(IBotConfiguration.ServiceId), config.ServiceId);
    builder.EmptyLine();
    
    builder.Heading("Capabilities", 2);
    AddCapabilityFromInterface(builder, nameof(IBotConfiguration.SupportsReplyChains), config.SupportsReplyChains);
    // ... uses reflection to read [MarkdownProperty] and [Description] attributes
    
    return builder.ToString();
}
```

## Value Formatting

The library intelligently formats different value types:

| Type | Format |
|------|--------|
| `bool` | True/False |
| `int`, `long`, etc. | Numeric string |
| `string` | As-is |
| `Enum` | Enum name |
| `IFormattable` | Uses Format property |
| Collections | "Collection (X items)" |
| Dictionaries | "Dictionary (X items)" |
| Complex objects | "TypeName (configured)" |

## Testing

Run markdown library tests:
```bash
dotnet test --filter "FullyQualifiedName~Markdown|FullyQualifiedName~BotConfiguration"
```

## Design Philosophy

**Explicit over Automatic**: Unlike reflection-heavy libraries that automatically include everything, this library requires you to explicitly choose what properties to include. This gives you:

1. **Full Control**: No surprises about what appears in output
2. **Flexibility**: Different views of the same object
3. **Performance**: Only processes what you request
4. **Maintainability**: Clear about what's exposed

Attributes are **optional metadata**, not automatic drivers. Use `PropertyWithAttributes()` when you want attribute values, or specify everything inline.

## License

Part of TheSexy6BotWorker project.
