using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using System.ComponentModel;
using TheSexy6BotWorker.Services.Voice;

namespace TheSexy6BotWorker.Commands;

/// <summary>
/// Discord slash commands for voice integration.
/// Allows users to summon the bot to voice channels and manage voice sessions.
/// </summary>
public class VoiceCommands
{
    private readonly ILogger<VoiceCommands> _logger;
    private readonly IVoiceSessionService _voiceSessionService;

    public VoiceCommands(ILogger<VoiceCommands> logger, IVoiceSessionService voiceSessionService)
    {
        _logger = logger;
        _voiceSessionService = voiceSessionService;
    }

    /// <summary>
    /// Summons the bot to the user's current voice channel.
    /// </summary>
    [Command("voice-join")]
    [Description("Summon the bot to your voice channel for AI conversation")]
    public async Task JoinVoiceAsync(CommandContext ctx)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            // Validate user is in a voice channel
            var member = ctx.Member;
            if (member == null)
            {
                await ctx.RespondAsync("❌ Could not retrieve member information.");
                return;
            }

            var guild = ctx.Guild;
            if (guild == null || !guild.Channels.TryGetValue(member.VoiceState?.ChannelId ?? 0, out var channel) || channel.Type != DiscordChannelType.Voice)
            {
                await ctx.RespondAsync("❌ You must be in a voice channel to use this command!");
                return;
            }

            _logger.LogInformation(
                "User {UserId} requested voice join in channel {ChannelId}",
                member.Id, channel.Id);

            // Create voice session
            var session = await _voiceSessionService.CreateSessionAsync(
                ctx.Guild!.Id,
                channel.Id,
                cts.Token);

            var maxDuration = session.Configuration.MaxSessionDurationMinutes;
            var costLimit = session.Configuration.CostLimitCents.HasValue
                ? $"\nCost limit: ${session.Configuration.CostLimitCents.Value / 100.0:F2}"
                : "";

            await ctx.RespondAsync(
                $"✅ Joined {channel.Name}! Speak naturally and I'll respond.\n" +
                $"Duration limit: {maxDuration} minutes{costLimit}");

            _logger.LogInformation(
                "Voice session {SessionId} created for guild {GuildId}",
                session.SessionId, ctx.Guild.Id);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already in a voice channel"))
        {
            await ctx.RespondAsync("❌ I'm already in a voice channel. Use `/voice-leave` first.");
            _logger.LogWarning(ex, "Attempted to join while already connected");
        }
        catch (UnauthorizedAccessException ex)
        {
            await ctx.RespondAsync("❌ I don't have permission to join that channel!");
            _logger.LogWarning(ex, "Permission denied for voice channel");
        }
        catch (OperationCanceledException)
        {
            await ctx.RespondAsync("❌ Command timed out. Please try again.");
            _logger.LogWarning("Voice join command timed out");
        }
        catch (Exception ex)
        {
            await ctx.RespondAsync($"❌ Failed to start voice session: {ex.Message}");
            _logger.LogError(ex, "Unexpected error in voice-join command");
        }
    }

    /// <summary>
    /// Disconnects the bot from the voice channel.
    /// </summary>
    [Command("voice-leave")]
    [Description("Disconnect the bot from the voice channel")]
    public async Task LeaveVoiceAsync(CommandContext ctx)
    {
        try
        {
            // Get active session for this guild
            var session = await _voiceSessionService.GetSessionAsync(ctx.Guild!.Id);

            if (session == null)
            {
                await ctx.RespondAsync("ℹ️ I'm not currently in a voice channel.");
                return;
            }

            _logger.LogInformation("Ending voice session {SessionId}", session.SessionId);

            // End the session
            var endedSession = await _voiceSessionService.EndSessionAsync(session.SessionId);

            // Calculate metrics
            var duration = DateTimeOffset.UtcNow - endedSession.StartTime;
            var messageCount = endedSession.ConversationContext.Count;

            var channelName = ctx.Guild.Channels.TryGetValue(endedSession.ChannelId, out var channel)
                ? channel.Name
                : "voice channel";

            await ctx.RespondAsync(
                $"👋 Left {channelName}.\n" +
                $"Session duration: {duration.Hours}h {duration.Minutes}m {duration.Seconds}s\n" +
                $"Messages exchanged: {messageCount}");

            _logger.LogInformation(
                "Voice session {SessionId} ended. Duration: {Duration}, Messages: {Messages}",
                endedSession.SessionId, duration, messageCount);
        }
        catch (Exception ex)
        {
            await ctx.RespondAsync($"❌ Error leaving voice channel: {ex.Message}");
            _logger.LogError(ex, "Unexpected error in voice-leave command");
        }
    }
}
