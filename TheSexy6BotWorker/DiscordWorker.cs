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
                services.AddHttpClient<PerplexitySearchService>(client =>
                    {
                        client.BaseAddress = new Uri("https://api.perplexity.ai");
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                            "Bearer", Guard.Against.NullOrEmpty(_configuration["Perplexity:ApiKey"], "Perplexity:ApiKey")
                        );
                    });
                services.AddSingleton<PerplexitySearchService>();
                services
                    .AddSingleton(sp =>
                    {
                        var kernelBuilder = Kernel.CreateBuilder();
                        kernelBuilder.AddGoogleAIGeminiChatCompletion(
                            modelId: "gemini-2.5-flash-lite",
                            apiKey: _configuration["GeminiKey"],
                            serviceId: "gemini");

                        kernelBuilder.AddOpenAIChatCompletion(
                            modelId: "grok-3-mini",
                            apiKey: _configuration["GrokKey"],
                            endpoint: new Uri("https://api.x.ai/v1/"),
                            serviceId: "grok");

                        var perplexityService = sp.GetRequiredService<PerplexitySearchService>();
                        kernelBuilder.Plugins.AddFromObject(perplexityService, "PerplexitySearchService");
    
                        return kernelBuilder.Build();
                    });

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

            DiscordActivity status = new("ready to rumble, baby.", DiscordActivityType.Competing);

            _client = builder.Build();

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
