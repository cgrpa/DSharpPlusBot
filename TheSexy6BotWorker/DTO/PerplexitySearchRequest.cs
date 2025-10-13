using System.Text.Json.Serialization;

namespace TheSexy6BotWorker.DTO
{
    public class PerplexitySearchRequest
    {
        public PerplexitySearchRequest(string query, string? country = null)
        {
            Query = query;
            Country = country;
        }

        [JsonPropertyName("query")]
        public string Query { get; set; }

        [JsonPropertyName("max_results")]
        public int MaxResults { get; set; } = 10;

        [JsonPropertyName("search_domain_filter")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

        public string[]? SearchDomainFilter { get; set; }


        [JsonPropertyName("max_tokens_per_page")]
        public int MaxTokensPerPage { get; set; } = 1024;

        [JsonPropertyName("country")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Country { get; set; }
    }
}