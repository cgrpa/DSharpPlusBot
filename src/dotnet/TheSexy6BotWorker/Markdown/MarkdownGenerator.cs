using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace TheSexy6BotWorker.Markdown;

/// <summary>
/// Fluent builder for generating markdown from objects with explicit property selection
/// </summary>
public class ObjectMarkdownBuilder<T> where T : notnull
{
    private readonly T _source;
    private readonly MarkdownBuilder _builder = new();
    
    public ObjectMarkdownBuilder(T source)
    {
        _source = source;
    }
    
    /// <summary>
    /// Adds a section with a heading
    /// </summary>
    public ObjectMarkdownBuilder<T> Section(string heading, int level = 2)
    {
        _builder.Heading(heading, level);
        return this;
    }
    
    /// <summary>
    /// Adds a section with properties configured via a fluent builder
    /// </summary>
    public ObjectMarkdownBuilder<T> Section(string heading, Action<PropertySectionBuilder<T>> configure, int level = 2)
    {
        _builder.Heading(heading, level);
        var sectionBuilder = new PropertySectionBuilder<T>(_source, _builder);
        configure(sectionBuilder);
        _builder.EmptyLine();
        return this;
    }
    
    /// <summary>
    /// Adds a property using expression-based selection
    /// </summary>
    public ObjectMarkdownBuilder<T> Property<TValue>(
        Expression<Func<T, TValue>> selector,
        string? label = null,
        string? icon = null,
        string? format = null,
        bool bold = true)
    {
        var value = selector.Compile()(_source);
        var propertyName = GetPropertyName(selector);
        var displayLabel = BuildLabel(label ?? propertyName, icon);
        var formattedValue = FormatValue(value, format);
        
        _builder.BulletPoint(displayLabel, formattedValue, bold);
        return this;
    }
    
    /// <summary>
    /// Adds a property using the [MarkdownProperty] attribute from the source object
    /// </summary>
    public ObjectMarkdownBuilder<T> PropertyWithAttributes<TValue>(Expression<Func<T, TValue>> selector)
    {
        var propertyInfo = GetPropertyInfo(selector);
        var attr = propertyInfo.GetCustomAttribute<MarkdownPropertyAttribute>();
        var descAttr = propertyInfo.GetCustomAttribute<DescriptionAttribute>();
        
        var value = selector.Compile()(_source);
        
        if (value == null && (attr?.SkipIfNull ?? true))
            return this;
        
        var label = attr?.Label ?? propertyInfo.Name;
        var displayLabel = BuildLabel(label, attr?.Icon);
        var formattedValue = FormatValue(value, attr?.Format);
        
        _builder.BulletPoint(displayLabel, formattedValue, attr?.Bold ?? true);
        return this;
    }
    
    /// <summary>
    /// Adds a custom bullet point with label and value
    /// </summary>
    public ObjectMarkdownBuilder<T> BulletPoint(string label, object? value, string? icon = null, bool bold = true)
    {
        if (value == null) return this;
        var displayLabel = BuildLabel(label, icon);
        _builder.BulletPoint(displayLabel, value, bold);
        return this;
    }
    
    /// <summary>
    /// Adds raw text
    /// </summary>
    public ObjectMarkdownBuilder<T> Text(string text)
    {
        _builder.Text(text);
        return this;
    }
    
    /// <summary>
    /// Adds an empty line
    /// </summary>
    public ObjectMarkdownBuilder<T> EmptyLine()
    {
        _builder.EmptyLine();
        return this;
    }
    
    /// <summary>
    /// Provides access to the underlying MarkdownBuilder for advanced operations
    /// </summary>
    public ObjectMarkdownBuilder<T> Custom(Action<MarkdownBuilder> configure)
    {
        configure(_builder);
        return this;
    }
    
    /// <summary>
    /// Reflects all properties of a nested object and adds them
    /// </summary>
    public ObjectMarkdownBuilder<T> ReflectObject<TObj>(TObj obj, Func<PropertyInfo, bool>? filter = null) where TObj : notnull
    {
        var type = obj.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .Where(p => !p.IsDefined(typeof(MarkdownIgnoreAttribute)))
            .Where(p => filter?.Invoke(p) ?? true)
            .OrderBy(p => p.Name);
        
        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(obj);
                if (value == null) continue;
                
                var formattedValue = FormatValue(value, null);
                if (!string.IsNullOrEmpty(formattedValue))
                {
                    _builder.BulletPoint(prop.Name, formattedValue);
                }
            }
            catch
            {
                // Skip properties that throw on access
            }
        }
        
        return this;
    }
    
    /// <summary>
    /// Builds the final markdown string
    /// </summary>
    public string Build() => _builder.ToString();
    
    public override string ToString() => Build();
    
    private static string GetPropertyName<TValue>(Expression<Func<T, TValue>> selector)
    {
        if (selector.Body is MemberExpression memberExpr)
            return memberExpr.Member.Name;
        
        if (selector.Body is UnaryExpression { Operand: MemberExpression unaryMemberExpr })
            return unaryMemberExpr.Member.Name;
        
        return "Property";
    }
    
    private static PropertyInfo GetPropertyInfo<TValue>(Expression<Func<T, TValue>> selector)
    {
        MemberExpression? memberExpr = selector.Body as MemberExpression;
        
        if (memberExpr == null && selector.Body is UnaryExpression unaryExpr)
            memberExpr = unaryExpr.Operand as MemberExpression;
        
        if (memberExpr?.Member is PropertyInfo propInfo)
            return propInfo;
        
        throw new ArgumentException("Expression must be a property access", nameof(selector));
    }
    
    private static string BuildLabel(string label, string? icon)
    {
        return string.IsNullOrEmpty(icon) ? label : $"{icon} {label}";
    }
    
    internal static string FormatValue(object? value, string? format)
    {
        if (value == null)
            return "null";
        
        if (!string.IsNullOrEmpty(format) && value is IFormattable formattable)
            return formattable.ToString(format, null);
        
        return value switch
        {
            bool b => b.ToString(),
            int or long or short or byte => value.ToString()!,
            float or double or decimal => value.ToString()!,
            string s => s,
            Enum e => e.ToString(),
            Type t => t.Name,
            IDictionary<string, object> dict => $"Dictionary ({dict.Count} items)",
            System.Collections.IEnumerable enumerable when value is not string =>
                $"Collection ({enumerable.Cast<object>().Count()} items)",
            _ when value.GetType().IsClass => $"{value.GetType().Name} (configured)",
            _ => value.ToString() ?? string.Empty
        };
    }
}

/// <summary>
/// Builder for adding properties within a section
/// </summary>
public class PropertySectionBuilder<T> where T : notnull
{
    private readonly T _source;
    private readonly MarkdownBuilder _builder;
    
    internal PropertySectionBuilder(T source, MarkdownBuilder builder)
    {
        _source = source;
        _builder = builder;
    }
    
    /// <summary>
    /// Adds a property using expression-based selection
    /// </summary>
    public PropertySectionBuilder<T> Property<TValue>(
        Expression<Func<T, TValue>> selector,
        string? label = null,
        string? icon = null,
        string? format = null,
        bool bold = true)
    {
        var compiled = selector.Compile();
        var value = compiled(_source);
        var propertyName = GetPropertyName(selector);
        var displayLabel = BuildLabel(label ?? propertyName, icon);
        var formattedValue = ObjectMarkdownBuilder<T>.FormatValue(value, format);
        
        _builder.BulletPoint(displayLabel, formattedValue, bold);
        return this;
    }
    
    /// <summary>
    /// Adds a custom bullet point
    /// </summary>
    public PropertySectionBuilder<T> BulletPoint(string label, object? value, string? icon = null, bool bold = true)
    {
        if (value == null) return this;
        var displayLabel = BuildLabel(label, icon);
        _builder.BulletPoint(displayLabel, value, bold);
        return this;
    }
    
    /// <summary>
    /// Conditionally adds a property
    /// </summary>
    public PropertySectionBuilder<T> PropertyIf<TValue>(
        bool condition,
        Expression<Func<T, TValue>> selector,
        string? label = null,
        string? icon = null)
    {
        if (condition)
            Property(selector, label, icon);
        return this;
    }
    
    private static string GetPropertyName<TValue>(Expression<Func<T, TValue>> selector)
    {
        if (selector.Body is MemberExpression memberExpr)
            return memberExpr.Member.Name;
        
        if (selector.Body is UnaryExpression { Operand: MemberExpression unaryMemberExpr })
            return unaryMemberExpr.Member.Name;
        
        return "Property";
    }
    
    private static string BuildLabel(string label, string? icon)
    {
        return string.IsNullOrEmpty(icon) ? label : $"{icon} {label}";
    }
}
