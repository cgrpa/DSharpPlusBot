
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TheSexy6BotWorker.DTOs
{
    public class PerplexitySearchResult
    {
        [JsonPropertyName("results")]
        [Description("The list of search results returned by the Perplexity API.")]
        public List<SearchResult>? Results { get; set; }
    }

    public class SearchResult
    {
        [JsonPropertyName("title")]
        [Description("The title of the search result.")]
        public string? Title { get; set; }

        [JsonPropertyName("url")]
        [Description("The URL of the search result.")]
        public string? Url { get; set; }
        [JsonPropertyName("snippet")]
        [Description("The snippet of the search result.")]
        public string? Snippet { get; set; }

        [JsonPropertyName("date")]

        [Description("The date of the search result.")]
        public string? Date { get; set; }

        [JsonPropertyName("last_updated")]
        [Description("The last updated date of the search result.")]
        public string? LastUpdated { get; set; }  
    }
}