using System.Buffers.Text;
using System.ComponentModel;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.DTOs;

namespace TheSexy6BotWorker.Services
{
    public class PerplexitySearchService
    {
        private readonly HttpClient _httpClient;
        private const string SearchEndpoint = "search";

        public PerplexitySearchService(HttpClient httpClient)
        {
            _httpClient = httpClient;

            // Verify the BaseAddress is set
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri("https://api.perplexity.ai/");
                Console.WriteLine("WARNING: HttpClient BaseAddress was null and had to be set manually!");
            }
            
        }


        [KernelFunction("perplexity_search")]
        [Description("Searches the Perplexity API with the given query and returns results.")]
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
                throw new ApplicationException($"Error occurred while searching Perplexity API: {ex.Message}", ex);
            }
        }
    }
}