# Quick Start: Discord Voice Integration

**Feature**: 001-voice-integration
**Target Audience**: Developers implementing the voice integration feature

## Prerequisites

Before starting implementation, ensure you have:

1. **.NET 9.0 SDK** installed
2. **Discord Bot Token** with voice permissions
3. **OpenAI API Key** with Realtime API access (beta)
4. **Native audio libraries** (libopus, libsodium, ffmpeg)
5. Access to the **OpenAI-CSharpRealtimeWPFDemo** project for reference

---

## Development Environment Setup

### 1. Install Native Dependencies

**macOS** (Development):
```bash
brew install opus libsodium ffmpeg
```

**Linux/Docker** (Production):
```dockerfile
RUN apt-get update && apt-get install -y \
    libopus0 libsodium23 ffmpeg
```

**Windows**: Automatically included via NuGet packages

### 2. Add NuGet Packages

```bash
cd TheSexy6BotWorker
dotnet add package DSharpPlus.VoiceNext --version 4.5.1
dotnet add package NAudio --version 2.2.1
```

### 3. Configure User Secrets

```bash
dotnet user-secrets set "OpenAI:RealtimeApiKey" "sk-proj-..."
dotnet user-secrets set "VoiceIntegration:DefaultBudgetPerServerUSD" "100"
dotnet user-secrets set "VoiceIntegration:MaxSessionDurationMinutes" "10"
```

---

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-2)

**Goal**: Bot can join/leave voice channels

**Tasks**:
1. Create `VoiceCommands.cs` with join/leave commands
2. Implement `VoiceSessionService.cs` (session management)
3. Register VoiceNext extension in `DiscordWorker.cs`
4. Write integration tests for voice connection lifecycle

**Success Criteria**:
- `/voice-join` connects bot to voice channel
- `/voice-leave` disconnects bot cleanly
- Resources properly cleaned up on disconnect

### Phase 2: OpenAI Integration (Weeks 3-4)

**Goal**: Bot can send/receive audio with OpenAI

**Tasks**:
1. Implement `OpenAIRealtimeClient.cs` (WebSocket client)
2. Create `AudioConverter.cs` (48kHz ↔ 24kHz resampling)
3. Implement bidirectional audio streaming
4. Add conversation context management

**Success Criteria**:
- Bot receives user speech from Discord
- Audio converted and sent to OpenAI
- Bot plays AI responses in Discord voice channel
- Conversation context maintained across exchanges

### Phase 3: Function Calling (Week 5)

**Goal**: AI can invoke Semantic Kernel plugins

**Tasks**:
1. Map existing plugins to OpenAI tool definitions
2. Implement function call execution flow
3. Add function result handling
4. Write integration tests for voice-triggered functions

**Success Criteria**:
- User can ask "What's the weather?" via voice
- AI calls `WeatherService.GetWeatherAsync()`
- AI responds with weather information via voice

### Phase 4: Production Hardening (Week 6)

**Goal**: System is reliable and cost-controlled

**Tasks**:
1. Implement error handling and reconnection logic
2. Add cost tracking and budget enforcement
3. Implement session timeouts and auto-disconnect
4. Add comprehensive logging and monitoring

**Success Criteria**:
- System recovers from network disconnections
- Cost limits enforced per server
- Sessions auto-disconnect after timeout
- All errors logged with context

---

## Key Implementation Points

### 1. VoiceNext Setup in DiscordWorker

```csharp
// DiscordWorker.cs
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // Existing Discord client setup...

    // NEW: Register VoiceNext extension
    var voiceNext = _discord.UseVoiceNext(new VoiceNextConfiguration
    {
        EnableIncoming = true,  // Required for receiving audio
        AudioFormat = AudioFormat.Default
    });

    // Register voice services
    services.AddSingleton<VoiceSessionService>();
    services.AddSingleton<OpenAIRealtimeClient>();
    services.AddTransient<AudioConverter>();

    await _discord.ConnectAsync();
}
```

### 2. Audio Format Conversion

```csharp
// AudioConverter.cs
public AudioFrame ToOpenAIFormat(AudioFrame discordFrame)
{
    // Discord: 48kHz stereo → OpenAI: 24kHz mono
    var resampler = new WaveFormat Converter(
        new WaveFormat(24000, 1),  // Target: 24kHz mono
        discordFrame.Data
    );

    return new AudioFrame(resampler.Read(), 24000, 1, 16);
}

public AudioFrame ToDiscordFormat(AudioFrame openaiFrame)
{
    // OpenAI: 24kHz mono → Discord: 48kHz stereo
    var resampler = new WaveFormatConverter(
        new WaveFormat(48000, 2),  // Target: 48kHz stereo
        openaiFrame.Data
    );

    return new AudioFrame(resampler.Read(), 48000, 2, 16);
}
```

### 3. WebSocket Event Handling

```csharp
// OpenAIRealtimeClient.cs
private async Task OnMessageReceivedAsync(string json)
{
    var message = JsonSerializer.Deserialize<OpenAIRealtimeMessage>(json);

    switch (message.Type)
    {
        case "response.audio.delta":
            var audioBytes = Convert.FromBase64String(message.Payload.delta);
            await ProcessAudioDeltaAsync(audioBytes);
            break;

        case "response.function_call_arguments.done":
            await ExecuteFunctionCallAsync(message.Payload);
            break;

        // Handle other events...
    }
}
```

### 4. Function Calling Integration

```csharp
// VoiceSessionService.cs
private async Task ExecuteFunctionAsync(string callId, string name, string argsJson)
{
    // Parse arguments
    var args = JsonSerializer.Deserialize<Dictionary<string, object>>(argsJson);

    // Execute via Semantic Kernel
    var result = await _kernel.InvokeAsync(name, new KernelArguments(args));

    // Send result back to OpenAI
    await _openAIClient.SendFunctionResultAsync(callId, result.ToString());
    await _openAIClient.CreateResponseAsync(); // Trigger AI response with result
}
```

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public void AudioConverter_ToOpenAIFormat_ConvertsCorrectly()
{
    // Arrange
    var discordFrame = new AudioFrame(
        data: new byte[3840],  // 20ms at 48kHz stereo
        sampleRate: 48000,
        channels: 2,
        bitsPerSample: 16
    );

    var converter = new AudioConverter();

    // Act
    var openaiFrame = converter.ToOpenAIFormat(discordFrame);

    // Assert
    Assert.Equal(24000, openaiFrame.SampleRate);
    Assert.Equal(1, openaiFrame.Channels);
    Assert.Equal(960, openaiFrame.Data.Length); // 20ms at 24kHz mono
}
```

### Integration Tests

```csharp
[Fact(Skip = "Integration")]
[Trait("Category", "Integration")]
public async Task VoiceSession_EndToEnd_CompletesSuccessfully()
{
    // Arrange
    var service = GetVoiceSessionService();
    var guildId = 123456789UL;
    var channelId = 987654321UL;

    // Act
    var session = await service.CreateSessionAsync(guildId, channelId);
    await service.SimulateUserSpeechAsync(session.SessionId, "Hello");
    var response = await service.WaitForAIResponseAsync(session.SessionId);
    await service.EndSessionAsync(session.SessionId);

    // Assert
    Assert.Equal(SessionState.Completed, session.State);
    Assert.NotNull(response);
}
```

---

## Debugging Tips

### 1. Enable Verbose Logging

```csharp
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
});
```

### 2. Monitor WebSocket Events

```csharp
_webSocket.StateChanged += (s, e) =>
{
    _logger.LogInformation("WebSocket state: {State}", e.State);
};
```

### 3. Inspect Audio Data

```csharp
// Log audio frame details
_logger.LogDebug("Audio frame: {SampleRate}Hz, {Channels}ch, {Bytes} bytes",
    frame.SampleRate, frame.Channels, frame.Data.Length);
```

### 4. Test with LOCAL_DEV Mode

```bash
LOCAL_DEV=true dotnet run
```

Then use `/test-voice-join` in Discord to test without affecting production commands.

---

## Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| `DllNotFoundException: opus` | Native library not installed | Install libopus: `brew install opus` |
| `WebSocketException: Unauthorized` | Invalid OpenAI API key | Check User Secrets configuration |
| Distorted audio | Incorrect format conversion | Verify sample rates (24kHz ↔ 48kHz) |
| Bot doesn't respond | Server VAD not configured | Enable turn_detection in session.update |
| High costs | No budget limits | Set VoiceIntegration:DefaultBudgetPerServerUSD |

---

## Reference Projects

### OpenAI-CSharpRealtimeWPFDemo

**Location**: `/Users/che/Documents/Visual Studio Code/TheSexy6BotWorker/OpenAI-CSharpRealtimeWPFDemo`

**Key Files to Study**:
- `MainWindow.xaml.cs`: WebSocket setup, event handling, audio streaming
- Audio capture logic (lines 452-480)
- Audio playback logic (lines 411-450)
- Function calling implementation (lines 159-256)

### TheSexy6BotWorker (Existing Code)

**Reference Points**:
- `DiscordWorker.cs`: DI setup, service registration
- `MessageCreatedHandler.cs`: Event handler pattern
- `WeatherService.cs`: Semantic Kernel plugin example
- `Program.cs`: Configuration and User Secrets

---

## Next Steps

After completing quickstart:
1. Review [research.md](research.md) for detailed technical findings
2. Study [data-model.md](data-model.md) for entity definitions
3. Review contracts in [contracts/](contracts/) for API specifications
4. Generate tasks with `/speckit.tasks` command

---

**Quickstart Status**: Complete ✓
**Estimated Implementation Time**: 6 weeks (1 developer)
**Ready for `/speckit.tasks` generation**: Yes
