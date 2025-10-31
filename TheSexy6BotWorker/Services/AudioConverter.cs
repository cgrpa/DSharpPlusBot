using NAudio.Wave;

namespace TheSexy6BotWorker.Services
{
    /// <summary>
    /// Converts audio between Discord VoiceNext format (48kHz stereo PCM) and OpenAI Realtime API format (24kHz mono PCM)
    /// </summary>
    public class AudioConverter
    {
        // Discord VoiceNext format: 48kHz, 16-bit, stereo
        private static readonly WaveFormat DiscordFormat = new(48000, 16, 2);

        // OpenAI Realtime API format: 24kHz, 16-bit, mono
        private static readonly WaveFormat OpenAIFormat = new(24000, 16, 1);

        /// <summary>
        /// Converts audio from Discord format (48kHz stereo) to OpenAI format (24kHz mono)
        /// </summary>
        /// <param name="discordAudio">PCM audio data in Discord format (48kHz, 16-bit, stereo)</param>
        /// <returns>PCM audio data in OpenAI format (24kHz, 16-bit, mono)</returns>
        public byte[] ConvertDiscordToOpenAI(byte[] discordAudio)
        {
            if (discordAudio.Length == 0)
                return Array.Empty<byte>();

            using var inputStream = new RawSourceWaveStream(discordAudio, 0, discordAudio.Length, DiscordFormat);

            // Convert stereo to mono by taking left channel only
            var monoStream = new StereoToMonoProvider16(inputStream);

            // Resample from 48kHz to 24kHz
            using var resampler = new MediaFoundationResampler(monoStream, OpenAIFormat);

            using var outputStream = new MemoryStream();
            var buffer = new byte[resampler.WaveFormat.AverageBytesPerSecond];
            int bytesRead;

            while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
            {
                outputStream.Write(buffer, 0, bytesRead);
            }

            return outputStream.ToArray();
        }

        /// <summary>
        /// Converts audio from OpenAI format (24kHz mono) to Discord format (48kHz stereo)
        /// </summary>
        /// <param name="openAIAudio">PCM audio data in OpenAI format (24kHz, 16-bit, mono)</param>
        /// <returns>PCM audio data in Discord format (48kHz, 16-bit, stereo)</returns>
        public byte[] ConvertOpenAIToDiscord(byte[] openAIAudio)
        {
            if (openAIAudio.Length == 0)
                return Array.Empty<byte>();

            using var inputStream = new RawSourceWaveStream(openAIAudio, 0, openAIAudio.Length, OpenAIFormat);

            // Resample from 24kHz to 48kHz
            using var resampler = new MediaFoundationResampler(inputStream, new WaveFormat(48000, 16, 1));

            // Convert mono to stereo by duplicating the channel
            var stereoStream = new MonoToStereoProvider16(resampler);

            using var outputStream = new MemoryStream();
            var buffer = new byte[DiscordFormat.AverageBytesPerSecond];
            int bytesRead;

            while ((bytesRead = stereoStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                outputStream.Write(buffer, 0, bytesRead);
            }

            return outputStream.ToArray();
        }
    }

    /// <summary>
    /// Converts stereo 16-bit PCM to mono by taking only the left channel
    /// </summary>
    internal class StereoToMonoProvider16 : IWaveProvider
    {
        private readonly IWaveProvider _sourceProvider;
        private readonly WaveFormat _monoFormat;

        public StereoToMonoProvider16(IWaveProvider sourceProvider)
        {
            if (sourceProvider.WaveFormat.Channels != 2)
                throw new ArgumentException("Source must be stereo");
            if (sourceProvider.WaveFormat.BitsPerSample != 16)
                throw new ArgumentException("Source must be 16-bit");

            _sourceProvider = sourceProvider;
            _monoFormat = new WaveFormat(sourceProvider.WaveFormat.SampleRate, 16, 1);
        }

        public WaveFormat WaveFormat => _monoFormat;

        public int Read(byte[] buffer, int offset, int count)
        {
            // Read twice as much data from source (stereo)
            var sourceBuffer = new byte[count * 2];
            var sourceBytesRead = _sourceProvider.Read(sourceBuffer, 0, sourceBuffer.Length);

            var samplesRead = sourceBytesRead / 4; // 4 bytes per stereo sample (2 channels * 2 bytes)

            // Extract left channel only
            for (int i = 0; i < samplesRead; i++)
            {
                // Copy left channel (first 2 bytes of each 4-byte stereo sample)
                buffer[offset + (i * 2)] = sourceBuffer[i * 4];
                buffer[offset + (i * 2) + 1] = sourceBuffer[i * 4 + 1];
            }

            return samplesRead * 2;
        }
    }

    /// <summary>
    /// Converts mono 16-bit PCM to stereo by duplicating the channel
    /// </summary>
    internal class MonoToStereoProvider16 : IWaveProvider
    {
        private readonly IWaveProvider _sourceProvider;
        private readonly WaveFormat _stereoFormat;

        public MonoToStereoProvider16(IWaveProvider sourceProvider)
        {
            if (sourceProvider.WaveFormat.Channels != 1)
                throw new ArgumentException("Source must be mono");
            if (sourceProvider.WaveFormat.BitsPerSample != 16)
                throw new ArgumentException("Source must be 16-bit");

            _sourceProvider = sourceProvider;
            _stereoFormat = new WaveFormat(sourceProvider.WaveFormat.SampleRate, 16, 2);
        }

        public WaveFormat WaveFormat => _stereoFormat;

        public int Read(byte[] buffer, int offset, int count)
        {
            // Ensure we read an even number of bytes (complete stereo samples)
            count = count / 4 * 4;

            var monoBuffer = new byte[count / 2];
            var monoBytesRead = _sourceProvider.Read(monoBuffer, 0, monoBuffer.Length);

            var samplesRead = monoBytesRead / 2; // 2 bytes per mono sample

            // Duplicate mono channel to both left and right
            for (int i = 0; i < samplesRead; i++)
            {
                var sample = (short)(monoBuffer[i * 2] | (monoBuffer[i * 2 + 1] << 8));

                // Left channel
                buffer[offset + (i * 4)] = (byte)(sample & 0xFF);
                buffer[offset + (i * 4) + 1] = (byte)((sample >> 8) & 0xFF);

                // Right channel (duplicate)
                buffer[offset + (i * 4) + 2] = (byte)(sample & 0xFF);
                buffer[offset + (i * 4) + 3] = (byte)((sample >> 8) & 0xFF);
            }

            return samplesRead * 4;
        }
    }
}
