using System.Text.Json.Serialization;

namespace TheSexy6BotWorker.DTOs
{
    public class WeatherResponse
    {
        [JsonPropertyName("current")]
        public CurrentWeather Current { get; set; } = new();
        
        [JsonPropertyName("daily")]
        public DailyWeather Daily { get; set; } = new();
        
        [JsonPropertyName("elevation")]
        public double Elevation { get; set; }
        
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }
        
        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
        
        [JsonPropertyName("timezone")]
        public string Timezone { get; set; } = string.Empty;
        
        public string? Error { get; set; }
        public string? Reason { get; set; }
    }

    public class CurrentWeather
    {
        [JsonPropertyName("temperature_2m")]
        public double Temperature { get; set; }
        
        [JsonPropertyName("relative_humidity_2m")]
        public int Humidity { get; set; }
        
        [JsonPropertyName("apparent_temperature")]
        public double ApparentTemperature { get; set; }
        
        [JsonPropertyName("weather_code")]
        public int WeatherCode { get; set; }
        
        [JsonPropertyName("wind_speed_10m")]
        public double WindSpeed { get; set; }
        
        [JsonPropertyName("wind_direction_10m")]
        public int WindDirection { get; set; }
        
        [JsonPropertyName("surface_pressure")]
        public double Pressure { get; set; }
        
        [JsonPropertyName("time")]
        public string Time { get; set; } = string.Empty;
    }

    public class DailyWeather
    {
        [JsonPropertyName("temperature_2m_max")]
        public List<double> MaxTemperatures { get; set; } = new();
        
        [JsonPropertyName("temperature_2m_min")]
        public List<double> MinTemperatures { get; set; } = new();
        
        [JsonPropertyName("sunrise")]
        public List<string> Sunrise { get; set; } = new();
        
        [JsonPropertyName("sunset")]
        public List<string> Sunset { get; set; } = new();
        
        [JsonPropertyName("time")]
        public List<string> Time { get; set; } = new();
    }

    // OpenMeteo geocoding response
    public class GeocodingResponse
    {
        [JsonPropertyName("results")]
        public List<GeocodingResult> Results { get; set; } = new();
    }

    public class GeocodingResult
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;
        
        [JsonPropertyName("country_code")]
        public string CountryCode { get; set; } = string.Empty;
        
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }
        
        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
        
        [JsonPropertyName("admin1")]
        public string? Admin1 { get; set; }
    }
}