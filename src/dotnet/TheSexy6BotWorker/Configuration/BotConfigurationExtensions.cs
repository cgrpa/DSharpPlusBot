using System.ComponentModel;
using System.Reflection;
using System.Text;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Contracts;
using TheSexy6BotWorker.Markdown;
using TheSexy6BotWorker.Models;

namespace TheSexy6BotWorker.Configuration;

public static class BotConfigurationExtensions
{
    private static readonly Type BotConfigurationType = typeof(IBotConfiguration);
    
    /// <summary>
    /// Generates a detailed description of the bot's configuration using the fluent markdown builder
    /// with attributes from IBotConfiguration
    /// </summary>
    public static string GenerateConfigurationDescription(this IBotConfiguration config)
    {
        var builder = new MarkdownBuilder();
        
        
        builder.Heading("Bot Configuration", 2);
        AddPropertyFromInterface(builder, config, nameof(IBotConfiguration.Prefix), config.Prefix);
        AddPropertyFromInterface(builder, config, nameof(IBotConfiguration.ServiceId), config.ServiceId);
        builder.EmptyLine();
        
        // Capabilities section - use descriptions from interface attributes
        builder.Heading("Capabilities", 2);
        AddCapabilityFromInterface(builder, nameof(IBotConfiguration.SupportsReplyChains), config.SupportsReplyChains);
        AddCapabilityFromInterface(builder, nameof(IBotConfiguration.SupportsFunctionCalling), config.SupportsFunctionCalling);
        AddCapabilityFromInterface(builder, nameof(IBotConfiguration.SupportsImages), config.SupportsImages);
        builder.EmptyLine();
        
        // Execution Settings section
        builder.Heading("Execution Settings", 2);
        var settingsAttr = GetMarkdownPropertyAttribute(nameof(IBotConfiguration.Settings));
        var settingsLabel = BuildLabel(settingsAttr?.Label ?? "Settings Type", settingsAttr?.Icon);
        builder.BulletPoint(settingsLabel, config.Settings.GetType().Name);
        
        // Reflect additional settings properties
        foreach (var prop in GetSettingsProperties(config.Settings))
        {
            try
            {
                var value = prop.GetValue(config.Settings);
                if (value != null)
                {
                    var formattedValue = FormatPropertyValue(value);
                    if (!string.IsNullOrEmpty(formattedValue))
                        builder.BulletPoint(prop.Name, formattedValue);
                }
            }
            catch { /* Skip properties that throw */ }
        }
        
        return builder.ToString();
    }
    
    private static void AddPropertyFromInterface(MarkdownBuilder builder, IBotConfiguration config, string propertyName, object value)
    {
        var attr = GetMarkdownPropertyAttribute(propertyName);
        var descAttr = GetDescriptionAttribute(propertyName);
        
        var label = BuildLabel(attr?.Label ?? propertyName, attr?.Icon);
        builder.BulletPoint(label, value);
    }
    
    private static void AddCapabilityFromInterface(MarkdownBuilder builder, string propertyName, bool isEnabled)
    {
        var attr = GetMarkdownPropertyAttribute(propertyName);
        var descAttr = GetDescriptionAttribute(propertyName);
        
        var label = BuildLabel(attr?.Label ?? propertyName, attr?.Icon);
        var status = isEnabled ? "✅ Enabled" : "❌ Disabled";
        var description = descAttr?.Description;
        
        // Include the description from the attribute if available
        if (!string.IsNullOrEmpty(description))
        {
            builder.BulletPoint(label, $"{status} - {description}");
        }
        else
        {
            builder.BulletPoint(label, status);
        }
    }
    
    private static MarkdownPropertyAttribute? GetMarkdownPropertyAttribute(string propertyName)
    {
        return BotConfigurationType
            .GetProperty(propertyName)?
            .GetCustomAttribute<MarkdownPropertyAttribute>();
    }
    
    private static DescriptionAttribute? GetDescriptionAttribute(string propertyName)
    {
        return BotConfigurationType
            .GetProperty(propertyName)?
            .GetCustomAttribute<DescriptionAttribute>();
    }
    
    private static string BuildLabel(string label, string? icon)
    {
        return string.IsNullOrEmpty(icon) ? label : $"{icon} {label}";
    }
    
    private static IEnumerable<PropertyInfo> GetSettingsProperties(PromptExecutionSettings settings)
    {
        var skipNames = new[] { "ExtensionData", "ModelId", "Metadata", "ServiceId", "FunctionChoiceBehavior" };
        
        return settings.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && !skipNames.Contains(p.Name))
            .OrderBy(p => p.Name);
    }
    
    private static string FormatPropertyValue(object value)
    {
        return value switch
        {
            null => "null",
            bool b => b.ToString(),
            int or long or short or byte => value.ToString()!,
            float or double or decimal => value.ToString()!,
            string s => $"\"{s}\"",
            Enum e => e.ToString(),
            IDictionary<string, object> dict => $"Dictionary with {dict.Count} items",
            System.Collections.IEnumerable enumerable when value is not string =>
                $"Collection with {enumerable.Cast<object>().Count()} items",
            _ when value.GetType().IsClass && value.GetType() != typeof(string) =>
                $"{value.GetType().Name} (configured)",
            _ => value.ToString() ?? string.Empty
        };
    }
}
