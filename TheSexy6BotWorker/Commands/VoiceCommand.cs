using DSharpPlus.Commands;
using DSharpPlus.Entities;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker.Commands
{
    /// <summary>
    /// Commands for voice channel interactions with AI
    /// </summary>
    public class VoiceCommand
    {
        private readonly VoiceService _voiceService;

        public VoiceCommand(VoiceService voiceService)
        {
            _voiceService = voiceService;
        }

        /// <summary>
        /// Joins the voice channel the user is currently in
        /// </summary>
        [Command("voice_join")]
        public async ValueTask JoinAsync(CommandContext context)
        {
            // Check if user is in a voice channel
            var voiceState = context.Member?.VoiceState;
            var channelId = voiceState?.ChannelId;

            if (channelId == null)
            {
                await context.RespondAsync("You need to be in a voice channel first!");
                return;
            }

            // Get the channel from guild
            var channel = await context.Guild!.GetChannelAsync(channelId.Value);
            if (channel == null)
            {
                await context.RespondAsync("Could not find your voice channel!");
                return;
            }

            var guildId = context.Guild!.Id;

            // Check if already in voice
            if (_voiceService.IsInVoiceChannel(guildId))
            {
                await context.RespondAsync("I'm already in a voice channel in this server!");
                return;
            }

            await context.RespondAsync($"Joining {channel.Name}...");

            var success = await _voiceService.JoinVoiceChannelAsync(context.Client, channel);

            if (success)
            {
                await context.FollowupAsync($"Connected to {channel.Name}! Start talking to chat with AI. I'll leave after 30 seconds of silence.");
            }
            else
            {
                await context.FollowupAsync("Failed to join voice channel. Check the logs for details.");
            }
        }

        /// <summary>
        /// Leaves the current voice channel
        /// </summary>
        [Command("voice_leave")]
        public async ValueTask LeaveAsync(CommandContext context)
        {
            var guildId = context.Guild!.Id;

            if (!_voiceService.IsInVoiceChannel(guildId))
            {
                await context.RespondAsync("I'm not in a voice channel!");
                return;
            }

            await _voiceService.LeaveVoiceChannelAsync(guildId);
            await context.RespondAsync("Left the voice channel. See you next time!");
        }
    }
}
