using System.Buffers.Text;
using TheSexy6BotWorker.DTO;

namespace TheSexy6BotWorker.Services
{
    public class PerplexitySearchService
    {
        private readonly HttpClient _httpClient;
        private const string SearchEndpoint = "search";

        public PerplexitySearchService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            
        }

        public async Task<PerplexitySearchResult> SearchAsync(PerplexitySearchRequest request)
        {
            try
            {
                var content = new StringContent
                (
                    System.Text.Json.JsonSerializer.Serialize(request),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(SearchEndpoint, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<PerplexitySearchResult>(responseContent,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return result;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while searching.", ex);
            }
        }
    }
}