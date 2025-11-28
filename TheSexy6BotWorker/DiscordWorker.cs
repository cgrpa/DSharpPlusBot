using Ardalis.GuardClauses;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheSexy6BotWorker.Commands;
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
                // Register IConfiguration so it's available in Discord's DI container
                services.AddSingleton<IConfiguration>(_configuration);

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

                // Register voice integration services
                services.AddTransient<Services.Voice.AudioConverter>();
                services.AddSingleton<Services.Voice.IVoiceSessionService, Services.Voice.VoiceSessionService>();

                // Register OpenAI Realtime client factory
                services.AddTransient<Services.Voice.IOpenAIRealtimeClient>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<Services.Voice.OpenAIRealtimeClient>>();
                    var config = sp.GetRequiredService<IConfiguration>();
                    var apiKey = config["OpenAiApiKey"] ?? throw new InvalidOperationException("OpenAIApiKey not configured");
                    return new Services.Voice.OpenAIRealtimeClient(logger, apiKey);
                });

            });

            builder.ConfigureEventHandlers(
                b => b.AddEventHandlers<Handlers.MessageCreatedHandler>(ServiceLifetime.Singleton));

            builder.UseCommands((IServiceProvider serviceProvider, CommandsExtension extension) =>
            {
                extension.AddCommands([typeof(PingCommand), typeof(Commands.VoiceCommands)]);
                TextCommandProcessor textCommandProcessor = new(new()
                {
                    PrefixResolver = new DefaultPrefixResolver(true, "/").ResolvePrefixAsync,
                });

                extension.AddProcessor(textCommandProcessor);
            });

            // Register VoiceNext extension for voice channel integration
            builder.UseVoiceNext(new VoiceNextConfiguration
            {
                EnableIncoming = true // Required for receiving user audio
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
