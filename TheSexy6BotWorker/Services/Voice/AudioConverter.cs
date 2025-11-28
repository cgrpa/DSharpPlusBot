using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using TheSexy6BotWorker.DTOs.Voice;

namespace TheSexy6BotWorker.Services.Voice;

/// <summary>
/// Converts audio between Discord format (48kHz stereo) and OpenAI format (24kHz mono).
/// Uses NAudio for sample rate conversion and channel mixing/splitting.
/// </summary>
public class AudioConverter
{
    private readonly ILogger<AudioConverter> _logger;

    public AudioConverter(ILogger<AudioConverter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts Discord audio format to OpenAI format.
    /// Discord: 48kHz, 2 channels (stereo), 16-bit PCM
    /// OpenAI: 24kHz, 1 channel (mono), 16-bit PCM
    /// </summary>
    /// <param name="discordFrame">Audio frame from Discord VoiceNext</param>
    /// <returns>Converted audio frame for OpenAI Realtime API</returns>
    public AudioFrame ToOpenAIFormat(AudioFrame discordFrame)
    {
        if (discordFrame.SampleRate != 48000 || discordFrame.Channels != 2)
        {
            throw new ArgumentException(
                $"Expected Discord format (48kHz stereo), got {discordFrame.SampleRate}Hz {discordFrame.Channels}ch");
        }

        try
        {
            // Create WaveFormat for Discord audio (48kHz stereo 16-bit)
            var discordFormat = new WaveFormat(48000, 16, 2);

            // Create wave provider from raw PCM bytes
            var rawSource = new RawSourceWaveStream(
                new MemoryStream(discordFrame.Data),
                discordFormat);

            // Convert to ISampleProvider for processing
            var sampleProvider = rawSource.ToSampleProvider();

            // Mix stereo to mono (average left and right channels)
            var monoProvider = new StereoToMonoSampleProvider(sampleProvider);

            // Resample from 48kHz to 24kHz
            var resampler = new WdlResamplingSampleProvider(monoProvider, 24000);

            // Convert back to 16-bit PCM
            var wave16 = new SampleToWaveProvider16(resampler);

            // Read resampled data into buffer
            using var outputStream = new MemoryStream();
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = wave16.Read(buffer, 0, buffer.Length)) > 0)
            {
                outputStream.Write(buffer, 0, bytesRead);
            }

            var convertedData = outputStream.ToArray();

            _logger.LogDebug(
                "Converted audio: {InputBytes} bytes (48kHz stereo) → {OutputBytes} bytes (24kHz mono)",
                discordFrame.Data.Length, convertedData.Length);

            return new AudioFrame
            {
                Data = convertedData,
                SampleRate = 24000,
                Channels = 1,
                BitsPerSample = 16,
                DurationMs = discordFrame.DurationMs,
                Timestamp = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert Discord audio to OpenAI format");
            throw;
        }
    }

    /// <summary>
    /// Converts OpenAI audio format to Discord format.
    /// OpenAI: 24kHz, 1 channel (mono), 16-bit PCM
    /// Discord: 48kHz, 2 channels (stereo), 16-bit PCM
    /// </summary>
    /// <param name="openaiFrame">Audio frame from OpenAI Realtime API</param>
    /// <returns>Converted audio frame for Discord VoiceTransmitSink</returns>
    public AudioFrame ToDiscordFormat(AudioFrame openaiFrame)
    {
        if (openaiFrame.SampleRate != 24000 || openaiFrame.Channels != 1)
        {
            throw new ArgumentException(
                $"Expected OpenAI format (24kHz mono), got {openaiFrame.SampleRate}Hz {openaiFrame.Channels}ch");
        }

        try
        {
            // Create WaveFormat for OpenAI audio (24kHz mono 16-bit)
            var openaiFormat = new WaveFormat(24000, 16, 1);

            // Create wave provider from raw PCM bytes
            var rawSource = new RawSourceWaveStream(
                new MemoryStream(openaiFrame.Data),
                openaiFormat);

            // Convert to ISampleProvider for processing
            var sampleProvider = rawSource.ToSampleProvider();

            // Resample from 24kHz to 48kHz
            var resampler = new WdlResamplingSampleProvider(sampleProvider, 48000);

            // Convert mono to stereo (duplicate channel for both left and right)
            var stereoProvider = new MonoToStereoSampleProvider(resampler);

            // Convert back to 16-bit PCM
            var wave16 = new SampleToWaveProvider16(stereoProvider);

            // Read resampled data into buffer
            using var outputStream = new MemoryStream();
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = wave16.Read(buffer, 0, buffer.Length)) > 0)
            {
                outputStream.Write(buffer, 0, bytesRead);
            }

            var convertedData = outputStream.ToArray();

            _logger.LogDebug(
                "Converted audio: {InputBytes} bytes (24kHz mono) → {OutputBytes} bytes (48kHz stereo)",
                openaiFrame.Data.Length, convertedData.Length);

            return new AudioFrame
            {
                Data = convertedData,
                SampleRate = 48000,
                Channels = 2,
                BitsPerSample = 16,
                DurationMs = openaiFrame.DurationMs,
                Timestamp = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert OpenAI audio to Discord format");
            throw;
        }
    }
}
