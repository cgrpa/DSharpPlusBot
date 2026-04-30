using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TheSexy6BotWorker.DTOs
{
    public class PerplexitySearchRequest
    {

        [JsonPropertyName("query")]
        [Description("The search query string.")]
        public string Query { get; set; }

        [JsonPropertyName("max_results")]
        [Description("The maximum number of search results to return.")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

        public int MaxResults { get; set; } = 10;

        [JsonPropertyName("search_domain_filter")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

        [Description("An optional filter for specific search domains.")]
        public string[]? SearchDomainFilter { get; set; }


        [JsonPropertyName("max_tokens_per_page")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

        public int MaxTokensPerPage { get; set; } = 1024;

        [JsonPropertyName("country")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [Description("The country code to tailor search results (e.g., 'US' for United States).")]
        public string? Country { get; set; }
    }
}