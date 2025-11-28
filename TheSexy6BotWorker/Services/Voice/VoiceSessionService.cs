using Ardalis.GuardClauses;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using TheSexy6BotWorker.DTOs.Voice;

namespace TheSexy6BotWorker.Services.Voice;

/// <summary>
/// Manages voice conversation sessions across Discord servers.
/// Singleton service that maintains active sessions and handles voice connections.
/// </summary>
public class VoiceSessionService : IVoiceSessionService
{
    private readonly ILogger<VoiceSessionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly DiscordClient _discordClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Guid, VoiceSessionState> _activeSessions;
    private readonly ConcurrentDictionary<ulong, Guid> _guildToSessionMap;
    private readonly ConcurrentDictionary<Guid, IOpenAIRealtimeClient> _openAIClients;

    public VoiceSessionService(
        ILogger<VoiceSessionService> logger,
        IConfiguration configuration,
        DiscordClient discordClient,
        IServiceProvider serviceProvider)
    {
        _logger = Guard.Against.Null(logger);
        _configuration = Guard.Against.Null(configuration);
        _discordClient = Guard.Against.Null(discordClient);
        _serviceProvider = Guard.Against.Null(serviceProvider);
        _activeSessions = new ConcurrentDictionary<Guid, VoiceSessionState>();
        _guildToSessionMap = new ConcurrentDictionary<ulong, Guid>();
        _openAIClients = new ConcurrentDictionary<Guid, IOpenAIRealtimeClient>();
    }

    /// <inheritdoc/>
    public async Task<VoiceSessionState> CreateSessionAsync(
        ulong guildId,
        ulong channelId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating voice session for guild {GuildId}, channel {ChannelId}", guildId, channelId);

        // Check if session already exists for this guild
        if (_guildToSessionMap.ContainsKey(guildId))
        {
            throw new InvalidOperationException(
                $"Bot is already in a voice channel in guild {guildId}. Use /voice-leave first.");
        }

        try
        {
            // Get the guild and channel
            var guild = await _discordClient.GetGuildAsync(guildId);
            if (!guild.Channels.TryGetValue(channelId, out var channel) || channel.Type != DiscordChannelType.Voice)
            {
                throw new ArgumentException($"Channel {channelId} is not a valid voice channel");
            }

            // Get VoiceNext extension
            var voiceNext = _discordClient.ServiceProvider.GetRequiredService<VoiceNextExtension>();
            if (voiceNext == null)
            {
                throw new InvalidOperationException("VoiceNext extension is not enabled");
            }

            // Connect to Discord voice channel
            _logger.LogDebug("Connecting to Discord voice channel {ChannelId}", channelId);
            var voiceConnection = await voiceNext.ConnectAsync(channel);

            // Create session configuration from app settings
            var config = new VoiceSessionConfig
            {
                MaxSessionDurationMinutes = _configuration.GetValue<int>("VoiceIntegration:MaxSessionDurationMinutes", 10),
                AutoDisconnectOnSilenceSeconds = _configuration.GetValue<int>("VoiceIntegration:AutoDisconnectOnSilenceSeconds", 300),
                EnableFunctionCalling = _configuration.GetValue<bool>("VoiceIntegration:EnableFunctionCalling", true),
                VoiceModel = _configuration.GetValue<string>("VoiceIntegration:VoiceModel", "alloy") ?? "alloy",
                Temperature = _configuration.GetValue<float>("VoiceIntegration:Temperature", 0.8f),
                MaxContextMessages = _configuration.GetValue<int>("VoiceIntegration:MaxContextMessages", 20),
                CostLimitCents = _configuration.GetValue<int?>("VoiceIntegration:CostLimitCents")
            };

            // Create session state
            var session = new VoiceSessionState
            {
                GuildId = guildId,
                ChannelId = channelId,
                State = SessionState.Connected,
                Configuration = config,
                ParticipantCount = channel.Users.Count
            };

            // Create and connect OpenAI Realtime client
            var openAIClient = _serviceProvider.GetRequiredService<IOpenAIRealtimeClient>();
            await openAIClient.ConnectAsync(config, cancellationToken);

            // Set up audio bridging event handlers
            openAIClient.AudioReceived += async (sender, audioData) =>
            {
                await OnOpenAIAudioReceivedAsync(session.SessionId, audioData);
            };

            openAIClient.ErrorOccurred += (sender, exception) =>
            {
                _logger.LogError(exception, "OpenAI error in session {SessionId}", session.SessionId);
            };

            // Store session and client
            _activeSessions[session.SessionId] = session;
            _guildToSessionMap[guildId] = session.SessionId;
            _openAIClients[session.SessionId] = openAIClient;

            // Update state to Active
            session.State = SessionState.Active;

            _logger.LogInformation(
                "Voice session {SessionId} created successfully. Guild: {GuildId}, Channel: {ChannelId}, " +
                "Participants: {ParticipantCount}, MaxDuration: {MaxDuration}min",
                session.SessionId, guildId, channelId, session.ParticipantCount,
                session.Configuration.MaxSessionDurationMinutes);

            return session;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied when joining voice channel {ChannelId}", channelId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create voice session for guild {GuildId}", guildId);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<VoiceSessionState?> GetSessionAsync(ulong guildId)
    {
        if (_guildToSessionMap.TryGetValue(guildId, out var sessionId))
        {
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult<VoiceSessionState?>(session);
            }
        }

        return Task.FromResult<VoiceSessionState?>(null);
    }

    /// <inheritdoc/>
    public async Task<VoiceSessionState> EndSessionAsync(Guid sessionId)
    {
        _logger.LogInformation("Ending voice session {SessionId}", sessionId);

        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            throw new ArgumentException($"Session {sessionId} not found");
        }

        try
        {
            // Update state to Disconnecting
            session.State = SessionState.Disconnecting;

            // Disconnect OpenAI client
            if (_openAIClients.TryRemove(sessionId, out var openAIClient))
            {
                _logger.LogDebug("Disconnecting from OpenAI Realtime API");
                await openAIClient.DisconnectAsync();
                openAIClient.Dispose();
            }

            // Get VoiceNext connection
            var voiceNext = _discordClient.ServiceProvider.GetRequiredService<VoiceNextExtension>();
            var guild = await _discordClient.GetGuildAsync(session.GuildId);
            var voiceConnection = voiceNext?.GetConnection(guild);

            if (voiceConnection != null)
            {
                _logger.LogDebug("Disconnecting from Discord voice channel");
                voiceConnection.Disconnect();
            }

            // Remove from active sessions
            _activeSessions.TryRemove(sessionId, out _);
            _guildToSessionMap.TryRemove(session.GuildId, out _);

            // Update final state
            session.State = SessionState.Completed;

            _logger.LogInformation(
                "Voice session {SessionId} ended successfully. Duration: {Duration}, Messages: {MessageCount}",
                sessionId,
                DateTimeOffset.UtcNow - session.StartTime,
                session.ConversationContext.Count);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending voice session {SessionId}", sessionId);
            session.State = SessionState.Error;
            throw;
        }
    }

    /// <summary>
    /// Handles audio received from OpenAI and sends it to Discord voice channel.
    /// </summary>
    private async Task OnOpenAIAudioReceivedAsync(Guid sessionId, byte[] pcm24kAudioData)
    {
        try
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                return;
            }

            // Get Discord voice connection
            var voiceNext = _discordClient.ServiceProvider.GetRequiredService<VoiceNextExtension>();
            var guild = await _discordClient.GetGuildAsync(session.GuildId);
            var voiceConnection = voiceNext?.GetConnection(guild);

            if (voiceConnection == null)
            {
                _logger.LogWarning("No voice connection for session {SessionId}", sessionId);
                return;
            }

            // Convert 24kHz mono PCM from OpenAI to 48kHz stereo for Discord
            var audioConverter = _serviceProvider.GetRequiredService<AudioConverter>();
            var openaiFrame = new AudioFrame
            {
                Data = pcm24kAudioData,
                SampleRate = 24000,
                Channels = 1,
                BitsPerSample = 16,
                DurationMs = (int)(pcm24kAudioData.Length / 2 / 24000.0 * 1000),
                Timestamp = DateTimeOffset.UtcNow
            };

            var discordFrame = audioConverter.ToDiscordFormat(openaiFrame);

            // Send to Discord
            var transmitStream = voiceConnection.GetTransmitSink();
            await transmitStream.WriteAsync(discordFrame.Data);
            await transmitStream.FlushAsync();

            _logger.LogTrace("Sent {Bytes} bytes of audio to Discord (session {SessionId})",
                discordFrame.Data.Length, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending audio to Discord for session {SessionId}", sessionId);
        }
    }
}

