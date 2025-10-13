
using Xunit;
using TheSexy6BotWorker.Services;
using Microsoft.Extensions.Configuration;
using Ardalis.GuardClauses;
using System.Net.Http.Headers;
using TheSexy6BotWorker.DTO;

namespace TheSexy6BotWorker.Tests.Services;

[Trait("Category", "Integration")]
public class PerplexityApiIntegrationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly PerplexitySearchService _service;
    private readonly IConfiguration _configuration;

    public PerplexityApiIntegrationTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets<PerplexityApiIntegrationTests>()
            .Build();

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.perplexity.ai/")
        };

        string apiKey = Guard.Against.NullOrEmpty(_configuration["Perplexity:ApiKey"], "API key not found in configuration.");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        _service = new PerplexitySearchService(_httpClient);
    }

    [Fact]
    public async Task SearchAsync_ValidQuery_ReturnsResults()
    {
        // Arrange
        var query = new PerplexitySearchRequest("What is the capital of France?");

        // Act
        var results = await _service.SearchAsync(query);

        // Assert
        Assert.NotNull(results.Results);
        //Assert.Contains(results, r => r.Text.Contains("Paris", StringComparison.OrdinalIgnoreCase));
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}