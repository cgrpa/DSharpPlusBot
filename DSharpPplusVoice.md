DSharpPlus provides voice functionality through its **VoiceNext** extension, which enables Discord bots to connect to voice channels, transmit audio, and receive audio data. Here's what you need to know about implementing voice features in your DSharpPlus bot.[1]

## Installation and Setup

To use voice features, install the `DSharpPlus.VoiceNext` NuGet package. After installation, enable VoiceNext on your `DiscordClient` before connecting:[1]

```csharp
using DSharpPlus.VoiceNext;

static VoiceNextClient voice;

// Enable VoiceNext
voice = discord.UseVoiceNext();
```

You'll also need FFmpeg installed in your application directory to handle audio encoding and decoding.[1]

## Connecting to Voice Channels

To connect your bot to a voice channel, you need to get the user's current voice channel and establish a connection:[1]

```csharp
var vnext = ctx.Client.GetVoiceNextClient();
var vnc = vnext.GetConnection(ctx.Guild);
var chn = ctx.Member?.VoiceState?.Channel;
vnc = await vnext.ConnectAsync(chn);
```

## Transmitting Audio

VoiceNext provides a transmit stream interface where you can write audio data that will be processed and sent to the voice channel. The audio data must be in raw PCM format (48kHz, 16-bit, stereo).[2][4][1]

## Receiving Audio

To receive incoming voice data, enable it in your VoiceNext configuration:[4][5]

```csharp
voice = discord.UseVoiceNext(new VoiceNextConfiguration
{
    EnableIncoming = true
});
```

The `VoiceNextConnection` provides two key events: `UserSpeaking` and `VoiceReceived`. The `VoiceReceived` event fires approximately every 20 milliseconds with PCM audio data.[3][5][4]

[1](https://dsharpplus.readthedocs.io/en/stable/articles/voicenext.html)
[2](https://dsharpplus.github.io/DSharpPlus/articles/audio/voicenext/transmit.html)
[3](https://dsharpplus.github.io/DSharpPlus/articles/audio/voicenext/receive.html)
[4](http://dsharpplus.readthedocs.io/en/latest/articles/voice/receive.html)
[5](https://dsharpplus.readthedocs.io/en/latest/articles/voice/receive.html)
[6](https://stackoverflow.com/questions/64336196/how-can-i-kick-member-from-voice-channel-dsharpplus)
[7](https://github.com/OoLunar/DSharpPlus.VoiceLink)
[8](https://dsharpplus.github.io/DSharpPlus/articles/basics/first_bot.html)
[9](https://www.reddit.com/r/csharp/comments/rjt8p9/dsharpplus_event_help/)
[10](https://www.nuget.org/profiles/DSharpPlus)