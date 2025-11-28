# Voice Command Contracts

**Feature**: 001-voice-integration
**Date**: 2025-11-25
**Type**: Discord Text Commands (DSharpPlus Command Framework)

## Overview

This document defines the command interface for voice integration. All commands follow DSharpPlus TextCommandProcessor pattern and are implemented as `[Command]` attributed methods.

---

## Command Prefix

**Development (LOCAL_DEV=true)**: `test-voice-`
**Production**: `voice-`

Example: `/test-voice-join` (dev) or `/voice-join` (prod)

---

## Commands

### 1. Join Voice Channel

**Command**: `voice-join` or `voice join`

**Description**: Summons the bot to the user's current voice channel and initiates a voice conversation session.

**Signature**:
```csharp
[Command("voice-join")]
[Aliases("voice join")]
[Description("Summon the bot to your voice channel for AI conversation")]
public async Task JoinVoiceAsync(CommandContext ctx)
```

**Parameters**: None (uses invoking user's current voice channel)

**Preconditions**:
- User MUST be in a voice channel
- Bot MUST have Connect and Speak permissions in the target channel
- Bot MUST NOT already be connected to a different channel in the same guild

**Success Response**:
```
✅ Joined {ChannelName}! Speak naturally and I'll respond.
Duration limit: {MaxSessionDurationMinutes} minutes
Cost limit: {CostLimitCents/100:C2} (if configured)
```

**Error Responses**:
| Error | Message | HTTP Equivalent |
|-------|---------|-----------------|
| User not in voice channel | ❌ You must be in a voice channel to use this command! | 400 Bad Request |
| Missing permissions | ❌ I don't have permission to join that channel! | 403 Forbidden |
| Already connected | ❌ I'm already in a voice channel. Use `/voice-leave` first. | 409 Conflict |
| Session creation failed | ❌ Failed to start voice session: {error} | 500 Internal Server Error |

**Example Usage**:
```
User: /voice-join
Bot: ✅ Joined General Voice! Speak naturally and I'll respond.
     Duration limit: 10 minutes

[User can now speak and receive AI responses]
```

---

### 2. Leave Voice Channel

**Command**: `voice-leave` or `voice leave`

**Description**: Disconnects the bot from the voice channel and ends the conversation session.

**Signature**:
```csharp
[Command("voice-leave")]
[Aliases("voice leave")]
[Description("Disconnect the bot from the voice channel")]
public async Task LeaveVoiceAsync(CommandContext ctx)
```

**Parameters**: None

**Preconditions**:
- Bot MUST be connected to a voice channel in the guild

**Success Response**:
```
👋 Left {ChannelName}.
Session duration: {duration}
Messages exchanged: {messageCount}
Estimated cost: ${cost:F2}
```

**Error Responses**:
| Error | Message | HTTP Equivalent |
|-------|---------|-----------------|
| Not connected | ℹ️ I'm not currently in a voice channel. | 404 Not Found |

**Example Usage**:
```
User: /voice-leave
Bot: 👋 Left General Voice.
     Session duration: 3m 24s
     Messages exchanged: 12
     Estimated cost: $1.14
```

---

### 3. Voice Session Status

**Command**: `voice-status`

**Description**: Displays information about the current voice session.

**Signature**:
```csharp
[Command("voice-status")]
[Description("Show current voice session information")]
public async Task VoiceStatusAsync(CommandContext ctx)
```

**Parameters**: None

**Preconditions**: None (can check even if not connected)

**Success Response (Connected)**:
```
🎤 Voice Session Active
Channel: {ChannelName}
Duration: {elapsed}
Participants: {count}
Messages: {messageCount}
Estimated cost: ${cost:F2}
Status: {SessionState}
```

**Success Response (Not Connected)**:
```
ℹ️ No active voice session in this server.
Use `/voice-join` to start a conversation.
```

**Example Usage**:
```
User: /voice-status
Bot: 🎤 Voice Session Active
     Channel: General Voice
     Duration: 1m 42s
     Participants: 3
     Messages: 7
     Estimated cost: $0.58
     Status: Active
```

---

### 4. Voice Configuration

**Command**: `voice-config`

**Description**: View or update voice session configuration for the server.

**Signature**:
```csharp
[Command("voice-config")]
[Description("Configure voice session settings for this server")]
[RequirePermissions(Permissions.Administrator)]
public async Task VoiceConfigAsync(
    CommandContext ctx,
    [Description("Setting name")] string? setting = null,
    [Description("New value")] string? value = null)
```

**Parameters**:
- `setting` (optional): Configuration key to view/update
- `value` (optional): New value for the setting

**Preconditions**:
- User MUST have Administrator permission in the guild

**Available Settings**:
| Setting | Type | Default | Range | Description |
|---------|------|---------|-------|-------------|
| `max-duration` | int | 10 | 1-30 | Maximum session duration (minutes) |
| `silence-timeout` | int | 300 | 30-600 | Auto-disconnect after silence (seconds) |
| `voice-model` | string | "alloy" | alloy/echo/fable/onyx/nova/shimmer | AI voice model |
| `enable-functions` | bool | true | true/false | Allow function calling |
| `cost-limit` | decimal? | null | >= 0 or null | Max cost per session (dollars) |
| `max-context` | int | 20 | 1-50 | Conversation history limit |

**Success Response (View All)**:
```
⚙️ Voice Configuration
max-duration: 10 minutes
silence-timeout: 300 seconds (5 minutes)
voice-model: alloy
enable-functions: true
cost-limit: None (unlimited)
max-context: 20 messages
```

**Success Response (Update)**:
```
✅ Updated voice-config.{setting} = {value}
```

**Error Responses**:
| Error | Message | HTTP Equivalent |
|-------|---------|-----------------|
| Invalid setting | ❌ Unknown setting: {setting}. Use `/voice-config` to see available settings. | 400 Bad Request |
| Invalid value | ❌ Invalid value for {setting}: {value}. Expected {type} in range {range}. | 400 Bad Request |
| No permission | ❌ You need Administrator permission to configure voice settings. | 403 Forbidden |

**Example Usage**:
```
User: /voice-config
Bot: ⚙️ Voice Configuration
     max-duration: 10 minutes
     silence-timeout: 300 seconds (5 minutes)
     voice-model: alloy
     enable-functions: true
     cost-limit: None (unlimited)
     max-context: 20 messages

User: /voice-config max-duration 15
Bot: ✅ Updated voice-config.max-duration = 15

User: /voice-config voice-model nova
Bot: ✅ Updated voice-config.voice-model = nova
```

---

### 5. Voice Usage Statistics

**Command**: `voice-stats`

**Description**: Display usage and cost statistics for voice sessions in the server.

**Signature**:
```csharp
[Command("voice-stats")]
[Description("View voice usage and cost statistics")]
[RequirePermissions(Permissions.Administrator)]
public async Task VoiceStatsAsync(
    CommandContext ctx,
    [Description("Time period")] string period = "month")
```

**Parameters**:
- `period` (optional): `today`, `week`, `month` (default: `month`)

**Preconditions**:
- User MUST have Administrator permission
- Requires cost tracking to be enabled (Phase 2)

**Success Response**:
```
📊 Voice Usage Stats (This Month)
Total sessions: {count}
Total duration: {duration}
Total messages: {messageCount}
Total cost: ${totalCost:F2}
Average cost/session: ${avgCost:F2}
Most active channel: {channelName} ({sessionCount} sessions)
```

**Error Responses**:
| Error | Message | HTTP Equivalent |
|-------|---------|-----------------|
| No data | ℹ️ No voice sessions recorded for this {period}. | 404 Not Found |
| Cost tracking disabled | ⚠️ Cost tracking is not enabled. Stats unavailable. | 501 Not Implemented |

**Example Usage**:
```
User: /voice-stats month
Bot: 📊 Voice Usage Stats (This Month)
     Total sessions: 47
     Total duration: 4h 23m
     Total messages: 234
     Total cost: $45.67
     Average cost/session: $0.97
     Most active channel: General Voice (29 sessions)
```

---

## Command Flow Diagrams

### Join Command Flow

```
User: /voice-join
  → VoiceCommands.JoinVoiceAsync()
    → Validate user in voice channel ✓
    → Validate bot permissions ✓
    → VoiceSessionService.GetOrCreateSessionAsync(guildId, channelId)
      → Check for existing session → None found
      → Create new VoiceSessionState
      → Connect to Discord voice channel
        → voiceNext.ConnectAsync(channel)
      → Connect to OpenAI Realtime API
        → OpenAIRealtimeClient.ConnectAsync(sessionId)
      → Return VoiceSessionState (State: Connected)
    → Respond to user with success message
```

### Leave Command Flow

```
User: /voice-leave
  → VoiceCommands.LeaveVoiceAsync()
    → VoiceSessionService.GetSessionAsync(guildId)
      → Found active session ✓
    → VoiceSessionService.EndSessionAsync(sessionId)
      → Set State: Disconnecting
      → Calculate session metrics (duration, cost, message count)
      → Disconnect from Discord voice
        → voiceConnection.Disconnect()
      → Disconnect from OpenAI
        → OpenAIRealtimeClient.DisconnectAsync()
      → Set State: Completed
      → Log session metrics
    → Respond with session summary
```

---

## Error Handling

### Command-Level Error Handling

All commands implement try-catch with structured logging:

```csharp
[Command("voice-join")]
public async Task JoinVoiceAsync(CommandContext ctx)
{
    try
    {
        // Validate preconditions
        var channel = ctx.Member?.VoiceState?.Channel;
        if (channel == null)
        {
            await ctx.RespondAsync("❌ You must be in a voice channel to use this command!");
            return;
        }

        // Execute command
        var session = await _voiceSessionService.CreateSessionAsync(ctx.Guild.Id, channel.Id);

        await ctx.RespondAsync($"✅ Joined {channel.Name}! Speak naturally and I'll respond.");
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("already connected"))
    {
        await ctx.RespondAsync("❌ I'm already in a voice channel. Use `/voice-leave` first.");
        _logger.LogWarning(ex, "Attempted to join while already connected");
    }
    catch (UnauthorizedAccessException ex)
    {
        await ctx.RespondAsync("❌ I don't have permission to join that channel!");
        _logger.LogWarning(ex, "Permission denied for channel {ChannelId}", channel.Id);
    }
    catch (Exception ex)
    {
        await ctx.RespondAsync($"❌ Failed to start voice session: {ex.Message}");
        _logger.LogError(ex, "Unexpected error in voice-join command");
    }
}
```

### Timeout Handling

Commands implement timeout protection:

```csharp
public async Task JoinVoiceAsync(CommandContext ctx)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    try
    {
        var session = await _voiceSessionService.CreateSessionAsync(
            ctx.Guild.Id,
            channel.Id,
            cts.Token
        );
    }
    catch (OperationCanceledException)
    {
        await ctx.RespondAsync("❌ Command timed out. Please try again.");
    }
}
```

---

## Permission Requirements

| Command | Required Permissions | Rationale |
|---------|---------------------|-----------|
| voice-join | User: None | Any user can start conversations |
| voice-join | Bot: Connect, Speak, Use Voice Activity | Required for voice channel operation |
| voice-leave | User: None | Any user who can join can leave |
| voice-status | User: None | Read-only information |
| voice-config | User: Administrator | Server-wide configuration changes |
| voice-stats | User: Administrator | Billing/cost information |

---

## Rate Limiting

To prevent abuse, commands implement cooldowns:

```csharp
[Command("voice-join")]
[Cooldown(1, 10, CooldownBucketType.User)] // 1 use per 10 seconds per user
public async Task JoinVoiceAsync(CommandContext ctx) { }

[Command("voice-config")]
[Cooldown(5, 60, CooldownBucketType.Guild)] // 5 uses per 60 seconds per server
public async Task VoiceConfigAsync(CommandContext ctx, ...) { }
```

---

## Testing Requirements

### Unit Tests

- Command validation logic (user in voice channel, permissions)
- Error message formatting
- Configuration parsing and validation

### Integration Tests

- Full command execution flow (join → speak → leave)
- Error scenarios (missing permissions, invalid config)
- Cooldown enforcement

### Manual Tests

- Commands in real Discord servers
- User permission scenarios
- Concurrent session management

---

**Contract Status**: Complete ✓
**Next Step**: Generate quickstart documentation for developers
