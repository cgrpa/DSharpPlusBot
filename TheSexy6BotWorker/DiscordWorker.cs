using Ardalis.GuardClauses;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheSexy6BotWorker.Commands;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Contracts;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker
{
    public class DiscordWorker : BackgroundService
    {
        private readonly ILogger<DiscordWorker> _logger;
        private readonly IConfiguration _configuration;
        private DiscordClient _client;
        public DiscordWorker(ILogger<DiscordWorker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Guard.Against.NullOrEmpty(_configuration["DiscordToken"], "DiscordToken");

            var builder = DiscordClientBuilder.CreateDefault(
                _configuration["DiscordToken"],
                DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents);


            builder.ConfigureServices(services =>
            {
                // Determine environment prefix for bot commands
                var environmentPrefix = Environment.GetEnvironmentVariable("Environment") == "Development" ? "" : "test-";

                // Register bot registry and configurations
                services.AddSingleton(sp =>
                {
                    var registry = new BotRegistry();
                    
                    // Register Gemini bot
                    registry.Register(new GeminiBotConfiguration(environmentPrefix));
                    
                    // Register Grok bot
                    registry.Register(new GrokBotConfiguration(environmentPrefix));
                    
                    return registry;
                });

                services.AddHttpClient<PerplexitySearchService>(client =>
                {
                    client.BaseAddress = new Uri("https://api.perplexity.ai");
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", Guard.Against.NullOrEmpty(_configuration["PerplexityApiKey"], "PerplexityApiKey")
                    );
                });

                // Add WeatherService with two HttpClients for OpenMeteo APIs
                services.AddHttpClient("WeatherClient", client =>
                {
                    client.BaseAddress = new Uri("https://api.open-meteo.com/v1/");
                });
                services.AddHttpClient("GeocodingClient", client =>
                {
                    client.BaseAddress = new Uri("https://geocoding-api.open-meteo.com/v1/");
                });
                services.AddTransient<WeatherService>(sp =>
                {
                    var weatherClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("WeatherClient");
                    var geocodingClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("GeocodingClient");
                    return new WeatherService(weatherClient, geocodingClient);
                });

                services
                    .AddSingleton(sp =>
                    {
                        var kernelBuilder = Kernel.CreateBuilder();
                        kernelBuilder.AddGoogleAIGeminiChatCompletion(
                            modelId: "gemini-2.5-flash-lite",
                            apiKey: _configuration["GeminiKey"],
                            serviceId: "gemini");

                        kernelBuilder.AddOpenAIChatCompletion(
                            modelId: "grok-4-fast-non-reasoning",
                            apiKey: _configuration["GrokKey"],
                            endpoint: new Uri("https://api.x.ai/v1/"),
                            serviceId: "grok");

                        var perplexityService = sp.GetRequiredService<PerplexitySearchService>();
                        kernelBuilder.Plugins.AddFromObject(perplexityService, "PerplexitySearchService");

                        var weatherService = sp.GetRequiredService<WeatherService>();
                        kernelBuilder.Plugins.AddFromObject(weatherService, "WeatherService");

                        return kernelBuilder.Build();
                    });

                // Add DynamicStatusService as singleton
                services.AddSingleton<DynamicStatusService>();
                
                // Add ConversationSessionManager for engagement mode
                services.AddSingleton<IConversationSessionManager, ConversationSessionManager>();

            });
            
            builder.ConfigureEventHandlers(
                b => b.AddEventHandlers<Handlers.MessageCreatedHandler>(ServiceLifetime.Singleton));

            builder.UseCommands((IServiceProvider serviceProvider, CommandsExtension extension) =>
            {
                extension.AddCommands([typeof(PingCommand)]);
                TextCommandProcessor textCommandProcessor = new(new()
                {
                    PrefixResolver = new DefaultPrefixResolver(true, "/").ResolvePrefixAsync,
                });

                extension.AddProcessor(textCommandProcessor);
            });

            _client = builder.Build();

            // Initialize dynamic status service
            var statusService = _client.ServiceProvider.GetRequiredService<DynamicStatusService>();
            statusService.Initialize(_client);

            DiscordActivity status = new("booting up my sarcasm module...", DiscordActivityType.Custom);
            await _client.ConnectAsync(status, DiscordUserStatus.Online);

            // Wait until cancelled
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client.DisconnectAsync();
            await base.StopAsync(cancellationToken);
        }
    }
}
