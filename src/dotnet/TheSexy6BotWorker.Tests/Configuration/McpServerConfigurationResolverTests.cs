using Microsoft.Extensions.Configuration;
using TheSexy6BotWorker.Configuration;

namespace TheSexy6BotWorker.Tests.Configuration;

public class McpServerConfigurationResolverTests
{
    [Fact]
    public void Resolve_UsesConfigurationValueBeforeEnvironmentVariable()
    {
        var options = CreateOptions(("Tavily", new McpServerOptions
        {
            Endpoint = "https://mcp.tavily.com/mcp",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer ${TavilyApiKey}"
            }
        }));

        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["TavilyApiKey"] = "config-key"
        });
        var environment = new DictionaryEnvironmentVariableProvider(new Dictionary<string, string?>
        {
            ["TavilyApiKey"] = "env-key"
        });

        var resolver = new McpServerConfigurationResolver(configuration, environment);
        var result = resolver.Resolve(options);

        var tavily = Assert.Single(result.ValidServers);
        Assert.Equal("Tavily", tavily.Key);
        Assert.Equal("Bearer config-key", tavily.Value.Headers["Authorization"]);
        Assert.Empty(result.SkippedServers);
    }

    [Fact]
    public void Resolve_UsesEnvironmentVariableFallbackWhenConfigurationValueMissing()
    {
        var options = CreateOptions(("Tavily", new McpServerOptions
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer ${TavilyApiKey}"
            }
        }));

        var resolver = new McpServerConfigurationResolver(
            BuildConfiguration(new Dictionary<string, string?>()),
            new DictionaryEnvironmentVariableProvider(new Dictionary<string, string?>
            {
                ["TavilyApiKey"] = "env-key"
            }));

        var result = resolver.Resolve(options);

        var tavily = Assert.Single(result.ValidServers);
        Assert.Equal("Bearer env-key", tavily.Value.Headers["Authorization"]);
        Assert.Empty(result.SkippedServers);
    }

    [Fact]
    public void Resolve_SkipsOnlyServerWithMissingInterpolatedHeaderValue()
    {
        var options = CreateOptions(
            ("Tavily", new McpServerOptions
            {
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Authorization"] = "Bearer ${TavilyApiKey}"
                }
            }),
            ("Weather", new McpServerOptions
            {
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["X-Source"] = "local"
                }
            }));

        var resolver = new McpServerConfigurationResolver(
            BuildConfiguration(new Dictionary<string, string?>()),
            new DictionaryEnvironmentVariableProvider(new Dictionary<string, string?>()));

        var result = resolver.Resolve(options);

        var weather = Assert.Single(result.ValidServers);
        Assert.Equal("Weather", weather.Key);

        var skipped = Assert.Single(result.SkippedServers);
        Assert.Equal("Tavily", skipped.ServerName);
        Assert.Equal(McpServerSkipReason.MissingInterpolatedValue, skipped.Reason);
        Assert.Contains("TavilyApiKey", skipped.MissingInterpolationKeys);
    }

    [Fact]
    public void Resolve_SkipsServerWhenDefaultParametersIsMalformedJson()
    {
        var options = CreateOptions(("Tavily", new McpServerOptions
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer static-key",
                ["DEFAULT_PARAMETERS"] = "{ \"topic\": "
            }
        }));

        var resolver = new McpServerConfigurationResolver(BuildConfiguration(new Dictionary<string, string?>()));
        var result = resolver.Resolve(options);

        Assert.Empty(result.ValidServers);
        var skipped = Assert.Single(result.SkippedServers);
        Assert.Equal("Tavily", skipped.ServerName);
        Assert.Equal(McpServerSkipReason.InvalidDefaultParametersJson, skipped.Reason);
    }

    [Fact]
    public void Resolve_AcceptsServerWhenDefaultParametersIsValidJsonAfterInterpolation()
    {
        var options = CreateOptions(("Tavily", new McpServerOptions
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer static-key",
                ["DEFAULT_PARAMETERS"] = "{ \"topic\": \"${TavilyTopic}\" }"
            }
        }));

        var resolver = new McpServerConfigurationResolver(
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["TavilyTopic"] = "weather"
            }),
            new DictionaryEnvironmentVariableProvider(new Dictionary<string, string?>()));

        var result = resolver.Resolve(options);

        var tavily = Assert.Single(result.ValidServers);
        Assert.Equal("{ \"topic\": \"weather\" }", tavily.Value.Headers["DEFAULT_PARAMETERS"]);
        Assert.Empty(result.SkippedServers);
    }

    private static McpOptions CreateOptions(params (string Name, McpServerOptions Server)[] servers)
    {
        var options = new McpOptions();
        foreach (var (name, server) in servers)
        {
            options.Servers[name] = server;
        }

        return options;
    }

    private static IConfiguration BuildConfiguration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private sealed class DictionaryEnvironmentVariableProvider(IDictionary<string, string?> values)
        : IEnvironmentVariableProvider
    {
        private readonly Dictionary<string, string?> _values = new(values, StringComparer.OrdinalIgnoreCase);

        public string? GetEnvironmentVariable(string variableName) => _values.GetValueOrDefault(variableName);
    }
}
