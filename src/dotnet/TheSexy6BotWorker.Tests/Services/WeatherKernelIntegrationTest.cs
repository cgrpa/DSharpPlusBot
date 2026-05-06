using Xunit;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Services;
using Microsoft.SemanticKernel.ChatCompletion;

namespace TheSexy6BotWorker.Tests.Services;

[Trait("Category", "Integration")]
public class WeatherKernelIntegrationTest : IDisposable
{
    private readonly Kernel _kernel;
    private readonly HttpClient _weatherClient;
    private readonly HttpClient _geocodingClient;

    public WeatherKernelIntegrationTest()
    {
        _weatherClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.open-meteo.com/v1/")
        };

        _geocodingClient = new HttpClient
        {
            BaseAddress = new Uri("https://geocoding-api.open-meteo.com/v1/")
        };

        var weatherService = new WeatherService(_weatherClient, _geocodingClient);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Plugins.AddFromObject(weatherService, "WeatherService");
        
        _kernel = kernelBuilder.Build();
    }

    [Fact]
    public async Task WeatherService_RegisteredAsPlugin_CanBeInvokedDirectly()
    {
        // Act - Invoke the weather function directly
        var result = await _kernel.InvokeAsync("WeatherService", "get_weather", new()
        {
            ["city"] = "London"
        });

        // Assert
        var weatherOutput = result.GetValue<string>();
        Assert.NotNull(weatherOutput);
        Assert.Contains("London", weatherOutput);
        Assert.Contains("Temperature:", weatherOutput);
        Assert.Contains("°C", weatherOutput);
    }

    [Fact]
    public async Task WeatherService_PluginFunctions_AreAvailable()
    {
        // Act - Check if the plugin functions are available
        var plugins = _kernel.Plugins;
        var weatherPlugin = plugins.FirstOrDefault(p => p.Name == "WeatherService");

        // Assert
        Assert.NotNull(weatherPlugin);
        Assert.Contains(weatherPlugin, f => f.Name == "get_weather");
        
        var weatherFunction = weatherPlugin.First(f => f.Name == "get_weather");
        Assert.Equal("Gets current weather information for a city. Returns temperature, description, humidity, wind speed, etc.", 
                    weatherFunction.Description);
    }

    [Theory]
    [InlineData("Paris")]
    [InlineData("Tokyo")]
    [InlineData("New York")]
    public async Task WeatherService_DifferentCities_ReturnsValidWeather(string city)
    {
        // Act
        var result = await _kernel.InvokeAsync("WeatherService", "get_weather", new()
        {
            ["city"] = city
        });

        // Assert
        var weatherOutput = result.GetValue<string>();
        Assert.NotNull(weatherOutput);
        Assert.Contains(city, weatherOutput);
        Assert.DoesNotContain("❌", weatherOutput); // Should not be an error
    }

    public void Dispose()
    {
        _weatherClient?.Dispose();
        _geocodingClient?.Dispose();
    }
}