using Xunit;
using TheSexy6BotWorker.Services;
using TheSexy6BotWorker.DTOs;

namespace TheSexy6BotWorker.Tests.Services;

[Trait("Category", "Integration")]
public class WeatherApiIntegrationTests : IDisposable
{
    private readonly HttpClient _weatherClient;
    private readonly HttpClient _geocodingClient;
    private readonly WeatherService _service;

    public WeatherApiIntegrationTests()
    {
        _weatherClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.open-meteo.com/v1/")
        };

        _geocodingClient = new HttpClient
        {
            BaseAddress = new Uri("https://geocoding-api.open-meteo.com/v1/")
        };

        _service = new WeatherService(_weatherClient, _geocodingClient);
    }

    [Fact]
    public async Task GetWeatherDataAsync_ValidCity_ReturnsWeatherData()
    {
        // Arrange
        var request = new WeatherRequest
        {
            City = "London",
            Units = "celsius"
        };

        // Act
        var result = await _service.GetWeatherDataAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Latitude > 51 && result.Latitude < 52); // London latitude range
        Assert.True(result.Longitude > -1 && result.Longitude < 1); // London longitude range
        Assert.NotNull(result.Current);
        Assert.True(result.Current.Temperature > -50 && result.Current.Temperature < 50); // Reasonable temperature range
        Assert.True(result.Current.Humidity >= 0 && result.Current.Humidity <= 100);
    }

    [Fact]
    public async Task GetWeatherAsync_ValidCity_ReturnsFormattedString()
    {
        // Arrange
        string city = "Paris";

        // Act
        var result = await _service.GetWeatherAsync(city);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Paris", result);
        Assert.Contains("Temperature:", result);
        Assert.Contains("°C", result);
        Assert.Contains("Humidity:", result);
        Assert.Contains("Wind:", result);
    }

    [Fact]
    public async Task GetWeatherAsync_InvalidCity_ReturnsErrorMessage()
    {
        // Arrange
        string invalidCity = "ThisCityDoesNotExist12345XYZ";

        // Act
        var result = await _service.GetWeatherAsync(invalidCity);

        // Assert
        Assert.Contains("Error getting weather", result);
        Assert.Contains("❌", result);
    }

    [Theory]
    [InlineData("celsius", "°C")]
    [InlineData("fahrenheit", "°F")]
    public async Task GetWeatherAsync_DifferentUnits_ReturnsCorrectUnit(string units, string expectedUnit)
    {
        // Arrange
        string city = "New York";

        // Act
        var result = await _service.GetWeatherAsync(city, units);

        // Assert
        Assert.Contains(expectedUnit, result);
    }

    [Fact]
    public async Task GetWeatherAsync_PopularCities_ReturnsValidData()
    {
        // Arrange
        var cities = new[] { "Tokyo", "Berlin", "Sydney", "Toronto" };

        foreach (var city in cities)
        {
            // Act
            var result = await _service.GetWeatherAsync(city);

            // Assert
            Assert.NotNull(result);
            Assert.Contains(city, result);
            Assert.DoesNotContain("❌", result); // Should not be an error
        }
    }

    public void Dispose()
    {
        _weatherClient?.Dispose();
        _geocodingClient?.Dispose();
    }
}