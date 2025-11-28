using Microsoft.Extensions.Logging.Abstractions;
using TheSexy6BotWorker.DTOs.Voice;
using TheSexy6BotWorker.Services.Voice;
using Xunit;

namespace TheSexy6BotWorker.Tests.Services.Voice;

public class AudioConverterTests
{
    private readonly AudioConverter _converter;

    public AudioConverterTests()
    {
        _converter = new AudioConverter(NullLogger<AudioConverter>.Instance);
    }

    [Fact]
    public void ToOpenAIFormat_ValidDiscordFrame_ConvertsCorrectly()
    {
        // Arrange - Create 20ms of Discord audio (48kHz stereo 16-bit)
        // 48000 samples/sec * 2 channels * 2 bytes/sample * 0.02 sec = 3840 bytes
        var discordFrame = new AudioFrame
        {
            Data = new byte[3840],
            SampleRate = 48000,
            Channels = 2,
            BitsPerSample = 16,
            DurationMs = 20
        };

        // Act
        var openaiFrame = _converter.ToOpenAIFormat(discordFrame);

        // Assert
        Assert.Equal(24000, openaiFrame.SampleRate);
        Assert.Equal(1, openaiFrame.Channels);
        Assert.Equal(16, openaiFrame.BitsPerSample);
        Assert.Equal(20, openaiFrame.DurationMs);

        // Expected size for 20ms at 24kHz mono 16-bit:
        // 24000 samples/sec * 1 channel * 2 bytes/sample * 0.02 sec = 960 bytes
        Assert.Equal(960, openaiFrame.Data.Length);
    }

    [Fact]
    public void ToDiscordFormat_ValidOpenAIFrame_ConvertsCorrectly()
    {
        // Arrange - Create 20ms of OpenAI audio (24kHz mono 16-bit)
        // 24000 samples/sec * 1 channel * 2 bytes/sample * 0.02 sec = 960 bytes
        var openaiFrame = new AudioFrame
        {
            Data = new byte[960],
            SampleRate = 24000,
            Channels = 1,
            BitsPerSample = 16,
            DurationMs = 20
        };

        // Act
        var discordFrame = _converter.ToDiscordFormat(openaiFrame);

        // Assert
        Assert.Equal(48000, discordFrame.SampleRate);
        Assert.Equal(2, discordFrame.Channels);
        Assert.Equal(16, discordFrame.BitsPerSample);
        Assert.Equal(20, discordFrame.DurationMs);

        // Expected size for 20ms at 48kHz stereo 16-bit:
        // 48000 samples/sec * 2 channels * 2 bytes/sample * 0.02 sec = 3840 bytes
        Assert.Equal(3840, discordFrame.Data.Length);
    }

    [Fact]
    public void ToOpenAIFormat_InvalidSampleRate_ThrowsArgumentException()
    {
        // Arrange - Invalid sample rate (not 48kHz)
        var invalidFrame = new AudioFrame
        {
            Data = new byte[1920],
            SampleRate = 44100, // Wrong sample rate
            Channels = 2,
            BitsPerSample = 16,
            DurationMs = 20
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _converter.ToOpenAIFormat(invalidFrame));
    }

    [Fact]
    public void ToOpenAIFormat_InvalidChannels_ThrowsArgumentException()
    {
        // Arrange - Invalid channel count (not stereo)
        var invalidFrame = new AudioFrame
        {
            Data = new byte[960],
            SampleRate = 48000,
            Channels = 1, // Wrong channel count
            BitsPerSample = 16,
            DurationMs = 20
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _converter.ToOpenAIFormat(invalidFrame));
    }

    [Fact]
    public void ToDiscordFormat_InvalidSampleRate_ThrowsArgumentException()
    {
        // Arrange - Invalid sample rate (not 24kHz)
        var invalidFrame = new AudioFrame
        {
            Data = new byte[960],
            SampleRate = 22050, // Wrong sample rate
            Channels = 1,
            BitsPerSample = 16,
            DurationMs = 20
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _converter.ToDiscordFormat(invalidFrame));
    }

    [Fact]
    public void ToDiscordFormat_InvalidChannels_ThrowsArgumentException()
    {
        // Arrange - Invalid channel count (not mono)
        var invalidFrame = new AudioFrame
        {
            Data = new byte[1920],
            SampleRate = 24000,
            Channels = 2, // Wrong channel count
            BitsPerSample = 16,
            DurationMs = 20
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _converter.ToDiscordFormat(invalidFrame));
    }

    [Fact]
    public void RoundTrip_DiscordToOpenAIToDiscord_MaintainsDuration()
    {
        // Arrange - Create 100ms of Discord audio
        var originalFrame = new AudioFrame
        {
            Data = new byte[19200], // 48kHz stereo 16-bit * 0.1 sec
            SampleRate = 48000,
            Channels = 2,
            BitsPerSample = 16,
            DurationMs = 100
        };

        // Act - Convert Discord → OpenAI → Discord
        var openaiFrame = _converter.ToOpenAIFormat(originalFrame);
        var finalFrame = _converter.ToDiscordFormat(openaiFrame);

        // Assert - Duration should be preserved
        Assert.Equal(originalFrame.DurationMs, openaiFrame.DurationMs);
        Assert.Equal(originalFrame.DurationMs, finalFrame.DurationMs);

        // Format should match original
        Assert.Equal(originalFrame.SampleRate, finalFrame.SampleRate);
        Assert.Equal(originalFrame.Channels, finalFrame.Channels);
        Assert.Equal(originalFrame.BitsPerSample, finalFrame.BitsPerSample);
    }
}
