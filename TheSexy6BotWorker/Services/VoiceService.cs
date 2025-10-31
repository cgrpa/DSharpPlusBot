using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace TheSexy6BotWorker.Services
{
    /// <summary>
    /// Orchestrates Discord voice connections with OpenAI Realtime API for voice conversations
    /// </summary>
    public class VoiceService
    {
        private readonly ILogger<VoiceService> _logger;
        private readonly AudioConverter _audioConverter;
        private readonly RealtimeService _realtimeService;
        private readonly ConcurrentDictionary<ulong, VoiceSessionState> _activeSessions = new();
        private readonly Timer _inactivityTimer;

        public VoiceService(
            ILogger<VoiceService> logger,
            AudioConverter audioConverter,
            RealtimeService realtimeService)
        {
            _logger = logger;
            _audioConverter = audioConverter;
            _realtimeService = realtimeService;

            // Check for inactive sessions every 10 seconds
            _inactivityTimer = new Timer(CheckInactiveSessions, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Joins a voice channel and starts a realtime conversation
        /// </summary>
        public async Task<bool> JoinVoiceChannelAsync(DiscordClient client, DiscordChannel channel)
        {
            try
            {
                if (channel.Type != DiscordChannelType.Voice && channel.Type != DiscordChannelType.Stage)
                {
                    _logger.LogWarning("Cannot join non-voice channel: {ChannelId}", channel.Id);
                    return false;
                }

                var guildId = channel.Guild.Id;

                // Check if already in a voice channel in this guild
                if (_activeSessions.ContainsKey(guildId))
                {
                    _logger.LogWarning("Already in a voice channel in guild {GuildId}", guildId);
                    return false;
                }

                var vnext = client.ServiceProvider.GetService<VoiceNextExtension>();
                if (vnext == null)
                {
                    _logger.LogError("VoiceNext is not enabled");
                    return false;
                }

                _logger.LogInformation("Connecting to voice channel {ChannelName} in guild {GuildName}",
                    channel.Name, channel.Guild.Name);

                _logger.LogDebug("Initiating VoiceNext handshake for guild {GuildId}", guildId);
                var connectTask = channel.ConnectAsync();
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(60)));

                if (completedTask != connectTask)
                {
                    _logger.LogError("Timed out while connecting to voice channel {ChannelName} in guild {GuildName} (waited 60s; voice events were received)",
                        channel.Name, channel.Guild.Name);
                    return false;
                }

                var connection = await connectTask;
                _logger.LogInformation("Voice gateway connected for guild {GuildId}", guildId);
                _logger.LogDebug("Voice connection established for guild {GuildId}", guildId);

                var sessionState = new VoiceSessionState
                {
                    GuildId = guildId,
                    ChannelId = channel.Id,
                    Connection = connection,
                    LastActivity = DateTime.UtcNow
                };

                _activeSessions.TryAdd(guildId, sessionState);

                // Start OpenAI Realtime API session
                var sessionId = $"guild_{guildId}";
                _logger.LogDebug("Starting realtime session {SessionId} for guild {GuildId}", sessionId, guildId);
                var sessionStarted = await _realtimeService.StartSessionAsync(
                    sessionId,
                    onAudioReceived: async (audioData) => await OnAIAudioReceivedAsync(guildId, audioData),
                    onError: async (error) => await OnRealtimeErrorAsync(guildId, error));

                if (!sessionStarted)
                {
                    _logger.LogError("Failed to start realtime session for guild {GuildId}; disconnecting voice", guildId);
                    _activeSessions.TryRemove(guildId, out _);
                    connection.Disconnect();
                    return false;
                }

                // Set up voice receive handler
                connection.VoiceReceived += async (sender, args) =>
                {
                    _logger.LogTrace(
                        "VoiceReceived fired for guild {GuildId} from user {UserId}; frameBytes={ByteCount}",
                        guildId,
                        args.User.Id,
                        args.PcmData.Length);

                    await OnDiscordAudioReceivedAsync(guildId, args.PcmData.ToArray(), args.User);
                };

                _logger.LogDebug("Subscribed to voice receive events for guild {GuildId}", guildId);

                _logger.LogInformation("Successfully joined voice channel and started realtime session for guild {GuildId}", guildId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join voice channel");
                return false;
            }
        }

        /// <summary>
        /// Leaves the voice channel in a guild
        /// </summary>
        public async Task LeaveVoiceChannelAsync(ulong guildId)
        {
            if (_activeSessions.TryRemove(guildId, out var sessionState))
            {
                try
                {
                    _logger.LogInformation("Leaving voice channel in guild {GuildId}", guildId);

                    // Stop OpenAI session
                    var sessionId = $"guild_{guildId}";
                    await _realtimeService.StopSessionAsync(sessionId);

                    // Disconnect from voice
                    sessionState.Connection?.Disconnect();

                    _logger.LogInformation("Successfully left voice channel in guild {GuildId}", guildId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error leaving voice channel in guild {GuildId}", guildId);
                }
            }
        }

        /// <summary>
        /// Gets the current voice connection for a guild
        /// </summary>
        public VoiceNextConnection? GetVoiceConnection(ulong guildId)
        {
            return _activeSessions.TryGetValue(guildId, out var state) ? state.Connection : null;
        }

        /// <summary>
        /// Checks if the bot is in a voice channel in the specified guild
        /// </summary>
        public bool IsInVoiceChannel(ulong guildId)
        {
            return _activeSessions.ContainsKey(guildId);
        }

        /// <summary>
        /// Handles audio received from Discord users
        /// </summary>
        private async Task OnDiscordAudioReceivedAsync(ulong guildId, byte[] pcmData, DiscordUser user)
        {
            try
            {
                if (!_activeSessions.TryGetValue(guildId, out var sessionState))
                    return;

                _logger.LogDebug(
                    "Received voice packet from user {UserId} in guild {GuildId}: {ByteCount} raw bytes",
                    user.Id,
                    guildId,
                    pcmData.Length);

                // Update last activity
                sessionState.LastActivity = DateTime.UtcNow;

                // Convert from Discord format (48kHz stereo) to OpenAI format (24kHz mono)
                var convertedAudio = _audioConverter.ConvertDiscordToOpenAI(pcmData);

                if (convertedAudio.Length > 0)
                {
                    _logger.LogDebug(
                        "Forwarding {ByteCount} converted bytes from guild {GuildId} to OpenAI session",
                        convertedAudio.Length,
                        guildId);

                    var sessionId = $"guild_{guildId}";
                    await _realtimeService.SendAudioAsync(sessionId, convertedAudio);
                }
                else
                {
                    _logger.LogTrace(
                        "Converted audio was empty for guild {GuildId} user {UserId}",
                        guildId,
                        user.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Discord audio for guild {GuildId}", guildId);
            }
        }

        /// <summary>
        /// Handles audio received from OpenAI Realtime API (AI speaking)
        /// </summary>
        private async Task OnAIAudioReceivedAsync(ulong guildId, byte[] audioData)
        {
            try
            {
                if (!_activeSessions.TryGetValue(guildId, out var sessionState))
                    return;

                _logger.LogDebug(
                    "Received AI audio payload for guild {GuildId}: {ByteCount} raw bytes",
                    guildId,
                    audioData.Length);

                // Update last activity
                sessionState.LastActivity = DateTime.UtcNow;

                // Convert from OpenAI format (24kHz mono) to Discord format (48kHz stereo)
                var convertedAudio = _audioConverter.ConvertOpenAIToDiscord(audioData);

                if (convertedAudio.Length > 0 && sessionState.Connection != null)
                {
                    _logger.LogDebug(
                        "Writing {ByteCount} bytes of AI audio to Discord voice connection for guild {GuildId}",
                        convertedAudio.Length,
                        guildId);

                    var transmitSink = sessionState.Connection.GetTransmitSink();
                    await transmitSink.WriteAsync(convertedAudio);
                }
                else
                {
                    _logger.LogTrace(
                        "AI audio chunk was empty for guild {GuildId}",
                        guildId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AI audio for guild {GuildId}", guildId);
            }
        }

        /// <summary>
        /// Handles errors from the Realtime API
        /// </summary>
        private async Task OnRealtimeErrorAsync(ulong guildId, Exception error)
        {
            _logger.LogError(error, "Realtime API error for guild {GuildId}", guildId);

            // Optionally disconnect on critical errors
            // await LeaveVoiceChannelAsync(guildId);
        }

        /// <summary>
        /// Checks for inactive sessions and disconnects them after 30 seconds
        /// </summary>
        private void CheckInactiveSessions(object? state)
        {
            foreach (var kvp in _activeSessions)
            {
                var guildId = kvp.Key;
                var sessionState = kvp.Value;
                var sessionId = $"guild_{guildId}";

                // Check if session has been inactive for more than 30 seconds
                if (_realtimeService.IsSessionInactive(sessionId, 30))
                {
                    _logger.LogInformation("Session for guild {GuildId} inactive for 30 seconds, disconnecting", guildId);

                    // Disconnect asynchronously (fire and forget)
                    _ = Task.Run(async () => await LeaveVoiceChannelAsync(guildId));
                }
            }
        }
    }

    /// <summary>
    /// Tracks the state of an active voice session
    /// </summary>
    internal class VoiceSessionState
    {
        public required ulong GuildId { get; init; }
        public required ulong ChannelId { get; init; }
        public required VoiceNextConnection Connection { get; init; }
        public DateTime LastActivity { get; set; }
    }
}
