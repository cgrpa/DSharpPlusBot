namespace TheSexy6BotWorker.DTOs
{
    public class WeatherRequest
    {
        public string City { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Units { get; set; } = "celsius"; // celsius or fahrenheit
    }
}