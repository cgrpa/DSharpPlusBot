namespace TheSexy6BotWorker.Services
{
    [Obsolete("Legacy Perplexity search has been removed. Use Tavily MCP search tools instead.")]
    public class PerplexitySearchService
    {
        public PerplexitySearchService(HttpClient _)
        {
            throw new NotSupportedException(
                "PerplexitySearchService is disabled. Search has migrated to Tavily MCP tools.");
        }
    }
}
