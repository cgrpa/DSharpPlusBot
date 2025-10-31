using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;

namespace TheSexy6BotWorker.Handlers
{
    public class VoiceEventsLogger :
        IEventHandler<VoiceStateUpdatedEventArgs>,
        IEventHandler<VoiceServerUpdatedEventArgs>
    {
        private readonly ILogger<VoiceEventsLogger> _logger;

        public VoiceEventsLogger(ILogger<VoiceEventsLogger> logger)
        {
            _logger = logger;
        }

        public Task HandleEventAsync(DiscordClient client, VoiceStateUpdatedEventArgs e)
        {
            var oldChannel = e.Before?.ChannelId?.ToString() ?? "null";
            var newChannel = e.After?.ChannelId?.ToString() ?? "null";
            _logger.LogInformation(
                "VoiceStateUpdated: old={Old} new={New}",
                oldChannel,
                newChannel);
            return Task.CompletedTask;
        }

        public Task HandleEventAsync(DiscordClient client, VoiceServerUpdatedEventArgs e)
        {
            _logger.LogInformation(
                "VoiceServerUpdated: guild={GuildId} endpoint={Endpoint}",
                e.Guild.Id,
                e.Endpoint);
            return Task.CompletedTask;
        }
    }
}
