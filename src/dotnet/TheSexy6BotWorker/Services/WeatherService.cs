using System.ComponentModel;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.DTOs;
using System.Web;

namespace TheSexy6BotWorker.Services
{
    public class WeatherService
    {
        private readonly HttpClient _weatherClient;
        private readonly HttpClient _geocodingClient;

        public WeatherService(HttpClient weatherClient, HttpClient geocodingClient)
        {
            _weatherClient = weatherClient;
            _geocodingClient = geocodingClient;
            
            // Set base addresses for OpenMeteo APIs
            if (_weatherClient.BaseAddress == null)
            {
                _weatherClient.BaseAddress = new Uri("https://api.open-meteo.com/v1/");
            }
            
            if (_geocodingClient.BaseAddress == null)
            {
                _geocodingClient.BaseAddress = new Uri("https://geocoding-api.open-meteo.com/v1/");
            }
        }

        [KernelFunction("get_weather")]
        [Description("Gets current weather information for a city. Returns temperature, description, humidity, wind speed, etc.")]
        public async Task<string> GetWeatherAsync(
            [Description("The city name to get weather for")] string city,
            [Description("Temperature units: 'celsius' or 'fahrenheit'. Default is celsius.")] string units = "celsius")
        {
            try
            {
                var request = new WeatherRequest
                {
                    City = city,
                    Units = units
                };

                var response = await GetWeatherDataAsync(request);
                return FormatWeatherResponse(response, request.City, units);
            }
            catch (Exception ex)
            {
                return $"❌ Error getting weather for {city}: {ex.Message}";
            }
        }

        public async Task<WeatherResponse> GetWeatherDataAsync(WeatherRequest request)
        {
            try
            {
                // First, get coordinates for the city
                var coordinates = await GetCoordinatesAsync(request.City);
                if (coordinates == null)
                {
                    throw new ApplicationException($"City '{request.City}' not found");
                }

                // Then get weather data using coordinates
                var tempUnit = request.Units.ToLower() == "fahrenheit" ? "fahrenheit" : "celsius";
                var url = $"forecast?latitude={coordinates.Latitude}&longitude={coordinates.Longitude}" +
                         $"&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m,wind_direction_10m,surface_pressure" +
                         $"&daily=temperature_2m_max,temperature_2m_min,sunrise,sunset" +
                         $"&temperature_unit={tempUnit}&wind_speed_unit=kmh&precipitation_unit=mm&timezone=auto";

                var response = await _weatherClient.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new ApplicationException($"Weather API returned {response.StatusCode}: {responseContent}");
                }

                var result = System.Text.Json.JsonSerializer.Deserialize<WeatherResponse>(responseContent,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return result ?? throw new ApplicationException("Failed to deserialize weather response");
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error occurred while fetching weather data: {ex.Message}", ex);
            }
        }

        private async Task<GeocodingResult?> GetCoordinatesAsync(string city)
        {
            var encodedCity = HttpUtility.UrlEncode(city);
            var url = $"search?name={encodedCity}&count=1&language=en&format=json";
            
            var response = await _geocodingClient.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new ApplicationException($"Geocoding API returned {response.StatusCode}");
            }

            var geocodingResponse = System.Text.Json.JsonSerializer.Deserialize<GeocodingResponse>(responseContent,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return geocodingResponse?.Results?.FirstOrDefault();
        }

        private static string FormatWeatherResponse(WeatherResponse weather, string cityName, string units)
        {
            var tempUnit = units.ToLower() == "fahrenheit" ? "°F" : "°C";
            
            var weatherDescription = GetWeatherDescription(weather.Current.WeatherCode);
            var weatherEmoji = GetWeatherEmoji(weather.Current.WeatherCode);
            
            var today = weather.Daily.Time.FirstOrDefault();
            var sunrise = weather.Daily.Sunrise.FirstOrDefault();
            var sunset = weather.Daily.Sunset.FirstOrDefault();
            var maxTemp = weather.Daily.MaxTemperatures.FirstOrDefault();
            var minTemp = weather.Daily.MinTemperatures.FirstOrDefault();

            // Format sunrise/sunset times
            var sunriseFormatted = DateTime.TryParse(sunrise, out var sr) ? sr.ToString("HH:mm") : "N/A";
            var sunsetFormatted = DateTime.TryParse(sunset, out var ss) ? ss.ToString("HH:mm") : "N/A";

            return $"""
                {weatherEmoji} **Weather in {cityName}**
                
                🌡️ **Temperature:** {weather.Current.Temperature:F1}{tempUnit} (feels like {weather.Current.ApparentTemperature:F1}{tempUnit})
                📊 **Conditions:** {weatherDescription}
                🔽 **Min/Max:** {minTemp:F1}{tempUnit} / {maxTemp:F1}{tempUnit}
                
                💨 **Wind:** {weather.Current.WindSpeed:F1} km/h from {weather.Current.WindDirection}°
                💧 **Humidity:** {weather.Current.Humidity}%
                🔽 **Pressure:** {weather.Current.Pressure:F0} hPa
                
                🌅 **Sunrise:** {sunriseFormatted} | 🌇 **Sunset:** {sunsetFormatted}
                📍 **Coordinates:** {weather.Latitude:F2}, {weather.Longitude:F2}
                """;
        }

        private static string GetWeatherDescription(int weatherCode)
        {
            return weatherCode switch
            {
                0 => "Clear sky",
                1 => "Mainly clear",
                2 => "Partly cloudy",
                3 => "Overcast",
                45 => "Fog",
                48 => "Depositing rime fog",
                51 => "Light drizzle",
                53 => "Moderate drizzle",
                55 => "Dense drizzle",
                56 => "Light freezing drizzle",
                57 => "Dense freezing drizzle",
                61 => "Slight rain",
                63 => "Moderate rain",
                65 => "Heavy rain",
                66 => "Light freezing rain",
                67 => "Heavy freezing rain",
                71 => "Slight snow fall",
                73 => "Moderate snow fall",
                75 => "Heavy snow fall",
                77 => "Snow grains",
                80 => "Slight rain showers",
                81 => "Moderate rain showers",
                82 => "Violent rain showers",
                85 => "Slight snow showers",
                86 => "Heavy snow showers",
                95 => "Thunderstorm",
                96 => "Thunderstorm with slight hail",
                99 => "Thunderstorm with heavy hail",
                _ => "Unknown conditions"
            };
        }

        private static string GetWeatherEmoji(int weatherCode)
        {
            return weatherCode switch
            {
                0 => "☀️",
                1 or 2 => "🌤️",
                3 => "☁️",
                45 or 48 => "🌫️",
                51 or 53 or 55 or 56 or 57 => "🌦️",
                61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "🌧️",
                71 or 73 or 75 or 77 or 85 or 86 => "🌨️",
                95 or 96 or 99 => "⛈️",
                _ => "🌤️"
            };
        }
    }
}