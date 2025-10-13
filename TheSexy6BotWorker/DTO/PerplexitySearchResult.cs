
using System.Text.Json.Serialization;

namespace TheSexy6BotWorker.DTO
{
    public class PerplexitySearchResult
    {
        [JsonPropertyName("query")]
        public string? Query { get; set; }
        
        [JsonPropertyName("max_results")]
        public int? MaxResults { get; set; }

        [JsonPropertyName("results")]
        public List<SearchResult>? Results { get; set; }
    }

    public class SearchResult
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        [JsonPropertyName("snippet")]
        public string? Snippet { get; set; }
        [JsonPropertyName("date")]
        public string? Date { get; set; }
        [JsonPropertyName("last_updated")]
        public string? LastUpdated { get; set; }  
    }
}