using Xunit;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Services;
using Microsoft.Extensions.Configuration;
using Ardalis.GuardClauses;
using System.Net.Http.Headers;
using TheSexy6BotWorker.DTOs;

namespace TheSexy6BotWorker.Tests.Services;

[Trait("Category", "Integration")]
public class PerplexityKernelIntegrationTest : IDisposable
{
    private readonly Kernel _kernel;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public PerplexityKernelIntegrationTest()
    {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets<PerplexityKernelIntegrationTest>()
            .AddEnvironmentVariables()
            .Build();

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.perplexity.ai/")
        };

        string apiKey = Guard.Against.NullOrEmpty(_configuration["PerplexityApiKey"], "API key not found in configuration.");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var perplexityService = new PerplexitySearchService(_httpClient);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Plugins.AddFromObject(perplexityService, "PerplexitySearchService");
        
        _kernel = kernelBuilder.Build();
    }

    [Fact]
    public async Task PerplexityService_RegisteredAsPlugin_CanBeInvokedDirectly()
    {
        // Arrange
        var request = new PerplexitySearchRequest
        {
            Query = "What is the capital of Japan?"
        };

        // Act - Invoke the perplexity search function directly
        var result = await _kernel.InvokeAsync("PerplexitySearchService", "perplexity_search", new()
        {
            ["request"] = request
        });

        // Assert
        var searchResult = result.GetValue<PerplexitySearchResult>();
        Assert.NotNull(searchResult);
        Assert.NotNull(searchResult.Results);
    }

    [Fact]
    public async Task PerplexityService_PluginFunctions_AreAvailable()
    {
        // Act - Check if the plugin functions are available
        var plugins = _kernel.Plugins;
        var perplexityPlugin = plugins.FirstOrDefault(p => p.Name == "PerplexitySearchService");

        // Assert
        Assert.NotNull(perplexityPlugin);
        Assert.Contains(perplexityPlugin, f => f.Name == "perplexity_search");
        
        var searchFunction = perplexityPlugin.First(f => f.Name == "perplexity_search");
        Assert.Equal("Searches the Perplexity API with the given query and returns results.", 
                    searchFunction.Description);
    }

    [Theory]
    [InlineData("What is artificial intelligence?")]
    [InlineData("Latest developments in quantum computing")]
    [InlineData("Climate change impact on polar bears")]
    public async Task PerplexityService_DifferentQueries_ReturnsValidResults(string query)
    {
        // Arrange
        var request = new PerplexitySearchRequest
        {
            Query = query
        };

        // Act
        var result = await _kernel.InvokeAsync("PerplexitySearchService", "perplexity_search", new()
        {
            ["request"] = request
        });

        // Assert
        var searchResult = result.GetValue<PerplexitySearchResult>();
        Assert.NotNull(searchResult);
        Assert.NotNull(searchResult.Results);
    }

    [Fact]
    public async Task PerplexityService_KernelPlugin_HasCorrectMetadata()
    {
        // Act
        var plugins = _kernel.Plugins;
        var perplexityPlugin = plugins.FirstOrDefault(p => p.Name == "PerplexitySearchService");

        // Assert
        Assert.NotNull(perplexityPlugin);
        Assert.Single(perplexityPlugin); // Should have exactly one function
        
        var searchFunction = perplexityPlugin.First();
        Assert.Equal("perplexity_search", searchFunction.Name);
        Assert.NotEmpty(searchFunction.Description);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}